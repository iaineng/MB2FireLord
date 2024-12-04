using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FireLord.Utils
{
    public class IniHelper
    {
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filepath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retval, int size, string filePath);

        [DllImport("kernel32", EntryPoint = "GetPrivateProfileString")]
        private static extern uint GetPrivateProfileStringA(string section, string key, string def, byte[] retVal, int size, string filePath);

        private readonly string _filePath;
        private readonly string _section;

        private Dictionary<string, string> _list = new Dictionary<string, string>();

        /// <summary>
        /// INI工具类
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="section"></param>
        public IniHelper(string filePath = "config.ini", string section = "default")
        {
            _filePath = filePath;

            _section = section;

            Reload();
        }

        /// <summary>
        /// 重新加载
        /// </summary>
        public void Reload()
        {
            _list = new Dictionary<string, string>();

            var keyList = _getKeyList();
            foreach (var key in keyList)
            {
                if (_list.ContainsKey(key))
                    _list[key] = Get(key);
                else
                    _list.Add(key, Get(key));
            }
        }

        /// <summary>
        /// 获取key列表
        /// </summary>
        /// <returns></returns>
        public string[] GetKeyList()
        {
            return _list.Keys.ToArray();
        }

        /// <summary>
        /// 获取所有KEY
        /// </summary>
        /// <returns></returns>
        private List<string> _getKeyList()
        {
            var result = new List<string>();
            var buf = new byte[65536];
            var len = GetPrivateProfileStringA(_section, null, null, buf, buf.Length, _filePath);

            var j = 0;
            for (var i = 0; i < len; i++)
                if (buf[i] == 0)
                {
                    result.Add(Encoding.Default.GetString(buf, j, i - j));
                    j = i + 1;
                }

            return result;
        }

        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        public string Get(string key, string defaultVal = "")
        {
            if (_list.TryGetValue(key, out var value))
            {
                return value;
            }

            var s = new StringBuilder(1024);
            GetPrivateProfileString(_section, key, defaultVal, s, 1024, _filePath);

            return s.ToString();
        }

        /// <summary>
        /// 设置值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        public void Set(string key, string val)
        {
            _list[key] = val;
            WritePrivateProfileString(_section, key, val, _filePath);
        }

        /// <summary>
        /// 删除key
        /// </summary>
        /// <param name="key"></param>
        public void Del(string key)
        {
            _list.Remove(key);
            WritePrivateProfileString(_section, key, null, _filePath);
        }

        /// <summary>
        /// 获取int
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        public int GetInt(string key, int defaultVal = 0)
        {
            var str = Get(key, defaultVal.ToString());

            var bo = int.TryParse(str, out var val);

            return bo ? val : defaultVal;
        }

        /// <summary>
        /// 获取float
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        public float GetFloat(string key, float defaultVal = 0)
        {
            var str = Get(key, defaultVal.ToString(CultureInfo.InvariantCulture));

            var bo = float.TryParse(str, out var val);

            return bo ? val : defaultVal;
        }

        /// <summary>
        /// 获取bool
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        public bool GetBool(string key, bool defaultVal = false)
        {
            var str = Get(key, defaultVal ? "1" : "0");

            return str == "1";
        }

        /// <summary>
        /// 设置int
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        public void SetInt(string key, int val)
        {
            Set(key, val.ToString());
        }

        /// <summary>
        /// 设置float
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        public void SetFloat(string key, float val)
        {
            Set(key, val.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 设置bool
        /// </summary>
        /// <param name="key"></param>
        /// <param name="bo"></param>
        public void SetBool(string key, bool bo)
        {
            Set(key, bo ? "1" : "0");
        }
    }
}
