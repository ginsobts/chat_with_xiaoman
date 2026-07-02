using System;
using UnityEngine;

namespace VN
{
    /// <summary>
    /// 从内存里的 WAV 字节解码成 AudioClip（用于加密打包后的语音）。
    /// 支持 PCM 8/16/24/32 位与 IEEE float32。
    /// </summary>
    public static class WavUtility
    {
        public static AudioClip ToAudioClip(byte[] data, string name)
        {
            try
            {
                if (data == null || data.Length < 44) return null;
                if (data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F') return null;
                if (data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E') return null;

                int channels = 1, sampleRate = 44100, bitsPerSample = 16, audioFormat = 1;
                int dataOffset = -1, dataLength = 0;

                int pos = 12;
                while (pos + 8 <= data.Length)
                {
                    string chunkId = new string(new[] { (char)data[pos], (char)data[pos + 1], (char)data[pos + 2], (char)data[pos + 3] });
                    int chunkSize = BitConverter.ToInt32(data, pos + 4);
                    int body = pos + 8;

                    if (chunkId == "fmt ")
                    {
                        audioFormat = BitConverter.ToInt16(data, body);
                        channels = BitConverter.ToInt16(data, body + 2);
                        sampleRate = BitConverter.ToInt32(data, body + 4);
                        bitsPerSample = BitConverter.ToInt16(data, body + 14);
                    }
                    else if (chunkId == "data")
                    {
                        dataOffset = body;
                        dataLength = chunkSize;
                    }

                    pos = body + chunkSize + (chunkSize % 2); // 块按偶数对齐
                    if (dataOffset >= 0 && chunkId == "data") break;
                }

                if (dataOffset < 0 || channels <= 0) return null;
                if (dataOffset + dataLength > data.Length) dataLength = data.Length - dataOffset;

                int bytesPerSample = bitsPerSample / 8;
                if (bytesPerSample <= 0) return null;
                int sampleCount = dataLength / bytesPerSample;
                float[] samples = new float[sampleCount];

                if (audioFormat == 3 && bitsPerSample == 32) // IEEE float
                {
                    for (int i = 0; i < sampleCount; i++)
                        samples[i] = BitConverter.ToSingle(data, dataOffset + i * 4);
                }
                else if (bitsPerSample == 16)
                {
                    for (int i = 0; i < sampleCount; i++)
                        samples[i] = BitConverter.ToInt16(data, dataOffset + i * 2) / 32768f;
                }
                else if (bitsPerSample == 24)
                {
                    for (int i = 0; i < sampleCount; i++)
                    {
                        int o = dataOffset + i * 3;
                        int v = (data[o] | (data[o + 1] << 8) | (data[o + 2] << 16));
                        if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000);
                        samples[i] = v / 8388608f;
                    }
                }
                else if (bitsPerSample == 32) // PCM 32
                {
                    for (int i = 0; i < sampleCount; i++)
                        samples[i] = BitConverter.ToInt32(data, dataOffset + i * 4) / 2147483648f;
                }
                else if (bitsPerSample == 8) // 无符号 8 位
                {
                    for (int i = 0; i < sampleCount; i++)
                        samples[i] = (data[dataOffset + i] - 128) / 128f;
                }
                else
                {
                    Debug.LogWarning("[WavUtility] 不支持的位深: " + bitsPerSample);
                    return null;
                }

                int frames = sampleCount / channels;
                if (frames <= 0) return null;

                var clip = AudioClip.Create(name, frames, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WavUtility] 解码失败 " + name + ": " + e.Message);
                return null;
            }
        }
    }
}
