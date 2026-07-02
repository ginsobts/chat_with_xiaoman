#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
批量为 story.json 的每句台词生成配音（对接 GPT-SoVITS api_v2）。

约定：输出文件名 = 节点 id，例如节点 n1 -> voice/n1.wav。
Unity 引擎里 VNView 会自动按「节点 id」去 StreamingAssets/voice/ 找同名音频，
所以本脚本跑完后不需要再改 story.json。

用法：
    1. 先启动 GPT-SoVITS 的 api_v2 服务（见 README.md）。
    2. 复制 config.example.json 为 config.json，填好参考音频等参数。
    3. python synthesize.py            # 生成所有缺失的配音
       python synthesize.py --list     # 只列出要生成的台词，不合成
       python synthesize.py --overwrite# 强制重新生成（覆盖已有）
"""

import argparse
import json
import os
import re
import sys
import time

try:
    import requests
except ImportError:
    print("缺少依赖 requests，请先运行： pip install -r requirements.txt")
    sys.exit(1)

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DEFAULT_STORY = os.path.join(PROJECT_ROOT, "Assets", "StreamingAssets", "story.json")
DEFAULT_OUT = os.path.join(PROJECT_ROOT, "Assets", "StreamingAssets", "voice")


def strip_json_comments(text):
    """去掉 // 与 /* */ 注释，同时正确跳过字符串内容。"""
    out = []
    i, n = 0, len(text)
    in_str = False
    while i < n:
        c = text[i]
        if in_str:
            out.append(c)
            if c == "\\" and i + 1 < n:
                out.append(text[i + 1])
                i += 2
                continue
            if c == '"':
                in_str = False
            i += 1
            continue
        if c == '"':
            in_str = True
            out.append(c)
            i += 1
            continue
        if c == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != "\n":
                i += 1
            continue
        if c == "/" and i + 1 < n and text[i + 1] == "*":
            i += 2
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                i += 1
            i += 2
            continue
        out.append(c)
        i += 1
    return "".join(out)


def load_config():
    cfg_path = os.path.join(HERE, "config.json")
    if not os.path.exists(cfg_path):
        print("找不到 config.json，请先复制 config.example.json 为 config.json 并填写。")
        sys.exit(1)
    with open(cfg_path, "r", encoding="utf-8") as f:
        return json.load(f)


def load_story(path):
    with open(path, "r", encoding="utf-8") as f:
        raw = f.read()
    return json.loads(strip_json_comments(raw))


def clean_text_for_tts(text, placeholder_name):
    """替换运行时变量占位符；去掉纯符号行不利于合成的情况由调用方判断。"""
    text = text.replace("%playername%", placeholder_name)
    # 去掉富文本标签（<b> 等），配音不需要
    text = re.sub(r"<[^>]+>", "", text)
    # 把换行合并成句读停顿
    text = text.replace("\n", "，")
    return text.strip()


def is_voiceable(node, cfg):
    text = (node.get("text") or "").strip()
    if not text:
        return False
    # 纯省略号 / 纯符号不值得配音
    if re.fullmatch(r"[…\.。·\-—\s]+", text):
        return False
    if cfg.get("onlySpeakerLines", True):
        speaker = (node.get("speaker") or "").strip()
        if not speaker:
            return False
        allow = cfg.get("speakerWhitelist")
        if allow and speaker not in allow:
            return False
    return True


def synth_one(cfg, text, out_path, session):
    url = cfg["apiUrl"].rstrip("/") + "/tts"
    payload = {
        "text": text,
        "text_lang": cfg.get("textLang", "zh"),
        "ref_audio_path": cfg["refAudioPath"],
        "prompt_text": cfg.get("promptText", ""),
        "prompt_lang": cfg.get("promptLang", "zh"),
        "text_split_method": cfg.get("textSplitMethod", "cut5"),
        "batch_size": cfg.get("batchSize", 1),
        "speed_factor": cfg.get("speedFactor", 1.0),
        "media_type": "wav",
        "streaming_mode": False,
    }
    # 可选：辅助参考音频、采样等
    for k in ("top_k", "top_p", "temperature", "aux_ref_audio_paths", "sample_steps"):
        cfgk = {"top_k": "topK", "top_p": "topP", "temperature": "temperature",
                "aux_ref_audio_paths": "auxRefAudioPaths", "sample_steps": "sampleSteps"}[k]
        if cfgk in cfg:
            payload[k] = cfg[cfgk]

    resp = session.post(url, json=payload, timeout=cfg.get("timeout", 300))
    ctype = resp.headers.get("Content-Type", "")
    if resp.status_code != 200 or "audio" not in ctype:
        # 出错时通常返回 JSON 说明
        detail = resp.text[:400]
        raise RuntimeError("HTTP {} {} -> {}".format(resp.status_code, ctype, detail))
    with open(out_path, "wb") as f:
        f.write(resp.content)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--story", default=DEFAULT_STORY)
    ap.add_argument("--out", default=None, help="输出目录，默认 StreamingAssets/voice")
    ap.add_argument("--list", action="store_true", help="只列出要合成的台词")
    ap.add_argument("--overwrite", action="store_true", help="覆盖已存在的音频")
    args = ap.parse_args()

    cfg = load_config()
    out_dir = args.out or cfg.get("outputDir") or DEFAULT_OUT
    os.makedirs(out_dir, exist_ok=True)
    placeholder_name = cfg.get("playerNamePlaceholder", "你")

    story = load_story(args.story)
    nodes = story.get("nodes", [])

    # 若配置了 linesFile（如日语翻译），直接按 {id: text} 合成，忽略 story.json 文本。
    lines_map = None
    lines_file = cfg.get("linesFile")
    if lines_file:
        lines_path = lines_file if os.path.isabs(lines_file) else os.path.join(HERE, lines_file)
        with open(lines_path, "r", encoding="utf-8") as f:
            lines_map = json.load(f)

    todo = []
    if lines_map is not None:
        # 用 story.json 的节点顺序排序，保证生成顺序稳定
        order = {n.get("id", ""): i for i, n in enumerate(nodes)}
        for nid, raw in sorted(lines_map.items(), key=lambda kv: order.get(kv[0], 1e9)):
            nid = nid.strip()
            if not nid or not (raw or "").strip():
                continue
            out_path = os.path.join(out_dir, nid + ".wav")
            text = clean_text_for_tts(raw, placeholder_name)
            todo.append((nid, nid, text, out_path))
    else:
        for node in nodes:
            if not is_voiceable(node, cfg):
                continue
            nid = node.get("id", "").strip()
            if not nid:
                continue
            voice_name = (node.get("voice") or "").strip() or nid
            out_path = os.path.join(out_dir, voice_name + ".wav")
            text = clean_text_for_tts(node["text"], placeholder_name)
            todo.append((nid, voice_name, text, out_path))

    if args.list:
        print("共 {} 句待配音：\n".format(len(todo)))
        for nid, vn, text, _ in todo:
            flag = " (含 %playername%)" if "%playername%" in (
                next((x.get("text", "") for x in nodes if x.get("id") == nid), "")) else ""
            print("[{}] {}{}".format(vn, text, flag))
        return

    overwrite = args.overwrite or cfg.get("overwrite", False)
    session = requests.Session()

    done, skipped, failed = 0, 0, 0
    for i, (nid, vn, text, out_path) in enumerate(todo, 1):
        if os.path.exists(out_path) and not overwrite:
            skipped += 1
            continue
        print("[{}/{}] {} -> {}".format(i, len(todo), vn, text))
        try:
            synth_one(cfg, text, out_path, session)
            done += 1
            time.sleep(cfg.get("delayBetween", 0.2))
        except Exception as e:
            failed += 1
            print("   !! 失败: {}".format(e))

    print("\n完成：生成 {}，跳过(已存在) {}，失败 {}。".format(done, skipped, failed))
    print("音频目录：{}".format(out_dir))
    if failed:
        print("有失败项，确认 api_v2 服务已启动、参考音频路径正确后可重跑。")


if __name__ == "__main__":
    main()
