using System.Text;

namespace VN
{
    /// <summary>
    /// 允许 story.json 写少量 // 或 /* */ 备注，读取前再转换为标准 JSON。
    /// </summary>
    public static class JsonCommentUtility
    {
        public static string StripComments(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            var output = new StringBuilder(json.Length);
            bool inString = false;
            bool escaping = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                char next = i + 1 < json.Length ? json[i + 1] : '\0';

                if (inString)
                {
                    output.Append(c);
                    if (escaping)
                    {
                        escaping = false;
                    }
                    else if (c == '\\')
                    {
                        escaping = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    output.Append(c);
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    i += 2;
                    while (i < json.Length && json[i] != '\n' && json[i] != '\r')
                        i++;
                    if (i < json.Length)
                        output.Append(json[i]);
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    i += 2;
                    while (i + 1 < json.Length && !(json[i] == '*' && json[i + 1] == '/'))
                    {
                        if (json[i] == '\n' || json[i] == '\r')
                            output.Append(json[i]);
                        i++;
                    }
                    i++;
                    continue;
                }

                output.Append(c);
            }

            return output.ToString();
        }
    }
}
