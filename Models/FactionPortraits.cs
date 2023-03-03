using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using HKW.Libs.Log4Cs;
using StarsectorTools.Libs.Utils;

namespace StarsectorToolsExtension.PortraitsManager
{
    public enum Gender
    {
        All,
        Male,
        Female,
    }

    /// <summary>
    /// 势力肖像
    /// </summary>
    public class FactionPortraits : IEnumerable, IEnumerable<string>
    {
        private const string strFactionExtension = ".faction";
        private const string strStandardMale = "standard_male";
        private const string strStandardFemale = "standard_female";
        private const string strPortraits = "portraits";
        private const string strDisplayName = "displayName";
        public string FactionId { get; private set; } = null!;
        public string? FactionName { get; private set; } = null;
        public string BaseDirectory { get; private set; } = null!;
        public string FileName { get; private set; } = null!;
        public string FileFullName { get; private set; } = null!;

        private HashSet<string> malePortraitsPath = new();
        public IReadOnlySet<string> MalePortraitsPath => malePortraitsPath;

        private readonly HashSet<string> femalePortraitsPath = new();
        public IReadOnlySet<string> FemalePortraitsPath => femalePortraitsPath;

        private HashSet<string> allPortraitsPath = new();
        public IReadOnlySet<string> AllPortraitsPath => allPortraitsPath;

        private Dictionary<string, string> allPortraitsName = new();
        public ReadOnlyDictionary<string, string> AllPortraitsName => new(allPortraitsName);

        private FactionPortraits(
            JsonObject jsonObject,
            JsonObject portraitsObject,
            string file,
            string baseDirectory,
            out string errMessage
        )
        {
            errMessage = string.Empty;
            FactionName = FactionId = Path.GetFileNameWithoutExtension(file);
            BaseDirectory = baseDirectory;
            FileName = Path.GetFileName(file);
            FileFullName = file;
            StringBuilder? errMale = GetMalePortraits(portraitsObject);
            StringBuilder? errFemale = GetFemalePortraits(portraitsObject);
            if (
                jsonObject.TryGetPropertyValue(strDisplayName, out var displayName)
                && displayName is not null
            )
                FactionName = displayName.GetValue<string>();
            if (errMale is not null || errFemale is not null)
                errMessage = $"肖像路径错误 文件: {file}{errMale}{errFemale}";
        }

        private StringBuilder? GetMalePortraits(JsonObject portraitsObject)
        {
            StringBuilder? errSB = new();
            if (
                !portraitsObject.TryGetPropertyValue(strStandardMale, out var maleNode)
                || maleNode?.AsArray() is not JsonArray maleArray
            )
                return errSB.AppendLine("\n男性肖像不存在");
            ;
            foreach (var item in maleArray)
            {
                string path = item!.GetValue<string>();
                if (!File.Exists(Path.Combine(BaseDirectory, path)))
                {
                    errSB.AppendLine($"\t{path}");
                    continue;
                }
                malePortraitsPath.Add(path);
                allPortraitsPath.Add(path);
                allPortraitsName.TryAdd(path, Path.GetFileNameWithoutExtension(path));
            }
            return errSB.Length > 0 ? errSB.Insert(0, "\n男性肖像路径错误:") : null;
        }

        private StringBuilder? GetFemalePortraits(JsonObject portraitsObject)
        {
            StringBuilder? errSB = new();
            if (
                !portraitsObject.TryGetPropertyValue(strStandardFemale, out var femaleNode)
                || femaleNode?.AsArray() is not JsonArray femaleArray
            )
                return errSB.AppendLine("\n女性肖像不存在");
            foreach (var item in femaleArray)
            {
                string path = item!.GetValue<string>();
                if (!File.Exists($"{BaseDirectory}\\{path}"))
                {
                    errSB.AppendLine($"\t{path}");
                    continue;
                }
                femalePortraitsPath.Add(path);
                allPortraitsPath.Add(path);
                allPortraitsName.TryAdd(path, Path.GetFileNameWithoutExtension(path));
            }
            return errSB.Length > 0 ? errSB.Insert(0, "\n女性肖像路径错误:") : null;
        }

        public static FactionPortraits? Create(
            string file,
            string baseDirectory,
            out string errMessage
        )
        {
            errMessage = string.Empty;
            if (
                Path.GetExtension(file) is not strFactionExtension
                || Utils.JsonParse2Object(file) is not JsonObject jsonObject
            )
                return null;
            if (
                !jsonObject.TryGetPropertyValue(strPortraits, out var portraitsNode)
                || portraitsNode?.AsObject() is not JsonObject portraitsObject
            )
                return null!;
            return new(jsonObject, portraitsObject, file, baseDirectory, out errMessage);
        }

        public bool Add(string path, Gender? gender = null)
        {
            var fullPath = Path.Combine(BaseDirectory, path);
            if (!File.Exists(fullPath) || allPortraitsPath.Contains(fullPath))
                return false;
            if (gender is Gender.Male)
            {
                allPortraitsPath.Add(path);
                allPortraitsName.TryAdd(path, Path.GetFileNameWithoutExtension(path));
                return malePortraitsPath.Add(path);
            }
            else if (gender is Gender.Female)
            {
                allPortraitsPath.Add(path);
                allPortraitsName.TryAdd(path, Path.GetFileNameWithoutExtension(path));
                return femalePortraitsPath.Add(path);
            }
            else
            {
                malePortraitsPath.Add(path);
                femalePortraitsPath.Add(path);
                allPortraitsName.TryAdd(path, Path.GetFileNameWithoutExtension(path));
                return allPortraitsPath.Add(path);
            }
        }

        public bool Remove(string path, Gender? gender = null)
        {
            if (gender is Gender.Male)
            {
                if (!femalePortraitsPath.Contains(path))
                {
                    allPortraitsPath.Remove(path);
                    allPortraitsName.Remove(path);
                }
                return malePortraitsPath.Remove(path);
            }
            else if (gender is Gender.Female)
            {
                if (!malePortraitsPath.Contains(path))
                {
                    allPortraitsPath.Remove(path);
                    allPortraitsName.Remove(path);
                }
                return femalePortraitsPath.Remove(path);
            }
            else
            {
                malePortraitsPath.Remove(path);
                femalePortraitsPath.Remove(path);
                allPortraitsName.Remove(path);
                return allPortraitsPath.Remove(path);
            }
        }

        public void Clear(Gender? gender = null)
        {
            if (gender is Gender.Male)
            {
                foreach (var path in malePortraitsPath)
                {
                    if (!femalePortraitsPath.Contains(path))
                    {
                        allPortraitsPath.Remove(path);
                        allPortraitsName.Remove(path);
                    }
                }
                malePortraitsPath.Clear();
            }
            else if (gender is Gender.Female)
            {
                foreach (var path in femalePortraitsPath)
                {
                    if (!malePortraitsPath.Contains(path))
                    {
                        allPortraitsPath.Remove(path);
                        allPortraitsName.Remove(path);
                    }
                }
                femalePortraitsPath.Clear();
            }
            else
            {
                malePortraitsPath.Clear();
                femalePortraitsPath.Clear();
                allPortraitsPath.Clear();
                allPortraitsName.Clear();
            }
        }

        public bool SaveTo(string file, bool createNew = false)
        {
            try
            {
                string portraitsData =
                    @$"	""portraits"":{{
    	""standard_male"":[
        	{string.Join(",\n\t\t\t", MalePortraitsPath.Select(s => @$"""{s.Replace("\\", "/")}"""))}
    	],
    	""standard_female"":[
        	{string.Join(",\n\t\t\t", FemalePortraitsPath.Select(s => @$"""{s.Replace("\\", "/")}"""))}
    	]
	}}";
                if (createNew)
                {
                    File.WriteAllText(file, $"{{\n{portraitsData}\n}}");
                }
                else
                {
                    string jsonData = File.ReadAllText(file);
                    if (
                        Path.GetExtension(file) is not strFactionExtension
                        || !Regex.IsMatch(jsonData, @"""portraits"":")
                    )
                        return false;
                    jsonData = Regex.Replace(
                        jsonData,
                        @"[ \t]*""portraits"":[^}]*}",
                        portraitsData
                    );
                    File.WriteAllText(file, jsonData);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("???", ex);
                return false;
            }
        }

        public IEnumerator<string> GetEnumerator() => AllPortraitsPath.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => AllPortraitsPath.GetEnumerator();
    }
}
