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

namespace StarsectorToolsExtension.PortraitsManager.Models
{
    /// <summary>
    /// 势力肖像
    /// </summary>
    public class FactionPortrait : IEnumerable, IEnumerable<string>
    {
        private const string _FactionExtension = ".faction";
        private const string _StandardMale = "standard_male";
        private const string _StandardFemale = "standard_female";
        private const string _Portraits = "portraits";
        private const string _DisplayName = "displayName";
        private const string _PortraitBaseData =
            "\t\"portraits\":{{\n"
            + "\t\t\"standard_male\":[\n"
            + "\t\t\t{0}\n"
            + "\t\t],\n"
            + "\t\t\"standard_female\":[\n"
            + "\t\t\t{1}\n"
            + "\t\t]\n"
            + "\t}}";

        public bool IsChanged { get; private set; } = false;
        public string FactionId { get; private set; } = null!;
        public string FactionName { get; private set; } = null;
        public string BaseDirectory { get; private set; } = null!;
        public string FileName { get; private set; } = null!;
        public string FileFullName { get; private set; } = null!;

        private readonly HashSet<string> _malePortraitsPath = new();
        public IReadOnlySet<string> MalePortraitsPath => _malePortraitsPath;

        private readonly HashSet<string> _femalePortraitsPath = new();
        public IReadOnlySet<string> FemalePortraitsPath => _femalePortraitsPath;

        private readonly HashSet<string> _allPortraitsPath = new();
        public IReadOnlySet<string> AllPortraitsPath => _allPortraitsPath;

        #region Ctor
        public FactionPortrait() { }

        private FactionPortrait(
            JsonObject jsonObject,
            JsonObject portraitsObject,
            string file,
            string baseDirectory,
            out string errMessage
        )
        {
            errMessage = string.Empty;
            FileFullName = file;
            FileName = Path.GetFileName(file);
            FactionName = FactionId = Path.GetFileNameWithoutExtension(FileName);
            BaseDirectory = baseDirectory;
            StringBuilder? errMale = GetMalePortraits(portraitsObject);
            StringBuilder? errFemale = GetFemalePortraits(portraitsObject);
            if (
                jsonObject.TryGetPropertyValue(_DisplayName, out var displayName)
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
                !portraitsObject.TryGetPropertyValue(_StandardMale, out var maleNode)
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
                _malePortraitsPath.Add(path);
                _allPortraitsPath.Add(path);
            }
            return errSB.Length > 0 ? errSB.Insert(0, "\n男性肖像路径错误:") : null;
        }

        private StringBuilder? GetFemalePortraits(JsonObject portraitsObject)
        {
            StringBuilder? errSB = new();
            if (
                !portraitsObject.TryGetPropertyValue(_StandardFemale, out var femaleNode)
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
                _femalePortraitsPath.Add(path);
                _allPortraitsPath.Add(path);
            }
            return errSB.Length > 0 ? errSB.Insert(0, "\n女性肖像路径错误:") : null;
        }
        #endregion
        public static FactionPortrait? Create(
            string file,
            string baseDirectory,
            out string errMessage
        )
        {
            errMessage = string.Empty;
            if (
                Path.GetExtension(file) is not _FactionExtension
                || Utils.JsonParse2Object(file) is not JsonObject jsonObject
            )
                return null;
            if (
                !jsonObject.TryGetPropertyValue(_Portraits, out var portraitsNode)
                || portraitsNode?.AsObject() is not JsonObject portraitsObject
            )
                return null!;
            return new(jsonObject, portraitsObject, file, baseDirectory, out errMessage);
        }

        public static void CreateTo(string file)
        {
            SaveToNewFile(file, string.Format(_PortraitBaseData, string.Empty, string.Empty));
        }

        public static FactionPortrait CreateTo(
            string file,
            string baseDirectory,
            out string errMessage
        )
        {
            if (File.Exists(file))
            {
                if (Create(file, baseDirectory, out errMessage) is FactionPortrait factionPortrait)
                    return factionPortrait;
            }
            SaveToNewFile(file, string.Format(_PortraitBaseData, string.Empty, string.Empty));
            return Create(file, baseDirectory, out errMessage)!;
        }

        public bool Add(string path, Gender gender = Gender.All)
        {
            var fullPath = Path.Combine(BaseDirectory, path);
            if (!File.Exists(fullPath) || _allPortraitsPath.Contains(fullPath))
                return false;
            IsChanged = true;
            if (gender is Gender.Male)
            {
                _allPortraitsPath.Add(path);
                return _malePortraitsPath.Add(path);
            }
            else if (gender is Gender.Female)
            {
                _allPortraitsPath.Add(path);
                return _femalePortraitsPath.Add(path);
            }
            else
            {
                _malePortraitsPath.Add(path);
                _femalePortraitsPath.Add(path);
                return _allPortraitsPath.Add(path);
            }
        }

        public bool Remove(string path, Gender gender = Gender.All)
        {
            IsChanged = true;
            if (gender is Gender.Male)
            {
                if (!_femalePortraitsPath.Contains(path))
                    _allPortraitsPath.Remove(path);
                return _malePortraitsPath.Remove(path);
            }
            else if (gender is Gender.Female)
            {
                if (!_malePortraitsPath.Contains(path))
                    _allPortraitsPath.Remove(path);
                return _femalePortraitsPath.Remove(path);
            }
            else
            {
                _malePortraitsPath.Remove(path);
                _femalePortraitsPath.Remove(path);
                return _allPortraitsPath.Remove(path);
            }
        }

        public void Clear(Gender gender = Gender.All)
        {
            IsChanged = true;
            if (gender is Gender.Male)
            {
                foreach (var path in _malePortraitsPath)
                {
                    if (!_femalePortraitsPath.Contains(path))
                        _allPortraitsPath.Remove(path);
                }
                _malePortraitsPath.Clear();
            }
            else if (gender is Gender.Female)
            {
                foreach (var path in _femalePortraitsPath)
                {
                    if (!_malePortraitsPath.Contains(path))
                        _allPortraitsPath.Remove(path);
                }
                _femalePortraitsPath.Clear();
            }
            else
            {
                _malePortraitsPath.Clear();
                _femalePortraitsPath.Clear();
                _allPortraitsPath.Clear();
            }
        }

        public bool Save() => SaveTo(FileFullName, false);

        public bool SaveTo(string file) => SaveTo(file, true);

        private bool SaveTo(string file, bool createNew = false)
        {
            try
            {
                string portraitsData = string.Format(
                    _PortraitBaseData,
                    string.Join(
                        ",\n\t\t\t",
                        MalePortraitsPath.Select(s => @$"""{s.Replace("\\", "/")}""")
                    ),
                    string.Join(
                        ",\n\t\t\t",
                        FemalePortraitsPath.Select(s => @$"""{s.Replace("\\", "/")}""")
                    )
                );
                if (createNew)
                {
                    SaveToNewFile(file, portraitsData);
                    return true;
                }
                return SaveToOriginalFile(file, portraitsData);
            }
            catch (Exception ex)
            {
                Logger.Error($"保存势力肖像时出现错误\n势力: {FactionName} 文件路径: {file}", ex);
                return false;
            }
        }

        private static bool SaveToOriginalFile(string file, string portraitsData)
        {
            string jsonData = File.ReadAllText(file);
            if (
                Path.GetExtension(file) is not _FactionExtension
                || !Regex.IsMatch(jsonData, @"""portraits"":")
            )
                return false;
            jsonData = Regex.Replace(jsonData, @"[ \t]*""portraits"":[^}]*}", portraitsData);
            File.WriteAllText(file, jsonData);
            return true;
        }

        private static void SaveToNewFile(string file, string portraitsData)
        {
            if (string.IsNullOrWhiteSpace(Path.GetExtension(file)))
                file += _FactionExtension;
            File.WriteAllText(file, $"{{\n{portraitsData}\n}}");
        }

        public static string? TryGetFactionPortraitData(string file) =>
            Regex.Match(File.ReadAllText(file), @"[ \t]*""portraits"":[^}]*}").Value;

        public static string CombineFactionPath(string baseDirectory, string faction) =>
            Path.Combine(baseDirectory, faction) + ".faction";

        public void Rename(string newFactionName)
        {
            var factionName = FactionName;
            FactionName = newFactionName;
            var newFileName = FileName.Replace(factionName, newFactionName);
            FileFullName = FileFullName.Replace(FileName, newFileName);
            FileName = newFileName;
            IsChanged = true;
        }

        public IEnumerator<string> GetEnumerator() => AllPortraitsPath.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => AllPortraitsPath.GetEnumerator();
    }
}
