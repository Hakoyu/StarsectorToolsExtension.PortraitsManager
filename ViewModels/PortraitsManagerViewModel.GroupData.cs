using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HKW.Libs.Log4Cs;
using HKW.ViewModels.Controls;
using HKW.ViewModels.Dialogs;
using StarsectorTools.Libs.GameInfo;
using StarsectorTools.Libs.Utils;
using StarsectorToolsExtension.PortraitsManager.Models;

namespace StarsectorToolsExtension.PortraitsManager.ViewModels
{
    internal partial class GroupData : ObservableObject
    {
        /// <summary>组Id</summary>
        public string GroupId { get; private set; }

        /// <summary>根目录</summary>
        public string BaseDirectory { get; private set; }

        /// <summary>势力目录</summary>
        public string FactionDirectory { get; private set; }

        /// <summary>拓展根目录</summary>
        public string PMDirectory { get; private set; }

        /// <summary>肖像目录</summary>
        public string PMPortraitsDirectory { get; private set; }

        /// <summary>肖像相对目录</summary>
        public string PMPortraitsDirectoryPath { get; private set; }

        /// <summary>备份目录</summary>
        public string PMBackupDirectory { get; private set; }

        /// <summary>势力备份目录</summary>
        public string PMFactionBackupDirectory { get; private set; }

        [ObservableProperty]
        private string _header = string.Empty;

        [ObservableProperty]
        private string _toolTip = string.Empty;

        [ObservableProperty]
        private bool _isExpanded = false;

        [ObservableProperty]
        private ListBoxVM _factionList = new();

        [ObservableProperty]
        private Dictionary<
            string,
            ObservableCollection<ListBoxItemVM>
        > _maleFactionPortraitsItem = new();

        [ObservableProperty]
        private Dictionary<
            string,
            ObservableCollection<ListBoxItemVM>
        > _femaleFactionPortraitsItem = new();

        private Dictionary<string, FactionPortrait> _allFactionPortraits = new();
        private Dictionary<string, Stream> _allImageStream = new();

        private HashSet<string> _planToDeletePortraitPaths = new();

        private GroupData(string groupId, string groupName, string baseDirectory)
        {
            Header = ToolTip = groupName;
            GroupId = groupId;
            BaseDirectory = baseDirectory;
            FactionDirectory = $"{baseDirectory}\\data\\world\\factions";
            PMDirectory =
                $"{baseDirectory}\\{nameof(StarsectorToolsExtension)}.PortraitsManager";
            PMPortraitsDirectory = $"{PMDirectory}\\Portraits";
            PMPortraitsDirectoryPath = $"{nameof(StarsectorToolsExtension)}.PortraitsManager\\Portraits";
            PMBackupDirectory = $"{PMDirectory}\\Backup";
            PMFactionBackupDirectory = $"{PMBackupDirectory}\\Faction";
            GetFactions(BaseDirectory, FactionDirectory);
        }

        public static GroupData? Create(string groupId, string groupName, string baseDirectory)
        {
            var temp = new GroupData(groupId, groupName, baseDirectory);
            if (temp._allFactionPortraits.Any())
                return temp;
            return null;
        }

        private void GetFactions(string baseDirectory, string factionDirectory)
        {
            if (Utils.GetAllSubFiles(factionDirectory) is not List<FileInfo> fileList)
                return;
            foreach (var file in fileList)
            {
                try
                {
                    if (
                        FactionPortrait.Create(file.FullName, baseDirectory, out var errMessage)
                        is not FactionPortrait factionPortrait
                    )
                        continue;
                    var faction = Path.GetFileNameWithoutExtension(file.FullName);
                    FactionList.Add(CreateFactionItem(faction, file.FullName));
                    var maleCollection = new ObservableCollection<ListBoxItemVM>();
                    var femaleCollection = new ObservableCollection<ListBoxItemVM>();
                    foreach (var portraitPath in factionPortrait.AllPortraitsPath)
                    {
                        var portraits = Path.GetFileNameWithoutExtension(portraitPath);
                        _allImageStream.TryAdd(
                            portraitPath,
                            new StreamReader(
                                Path.Combine(factionPortrait.BaseDirectory, portraitPath)
                            ).BaseStream
                        );
                        if (factionPortrait.MalePortraitsPath.Contains(portraitPath))
                            maleCollection.Add(
                                CreatePortraitItem(
                                    Gender.Male,
                                    faction,
                                    portraits,
                                    portraitPath,
                                    _allImageStream[portraitPath]
                                )
                            );
                        if (factionPortrait.FemalePortraitsPath.Contains(portraitPath))
                            femaleCollection.Add(
                                CreatePortraitItem(
                                    Gender.Female,
                                    faction,
                                    portraits,
                                    portraitPath,
                                    _allImageStream[portraitPath]
                                )
                            );
                    }
                    _allFactionPortraits.Add(faction, factionPortrait);
                    MaleFactionPortraitsItem.Add(faction, maleCollection);
                    FemaleFactionPortraitsItem.Add(faction, femaleCollection);
                }
                catch (Exception ex)
                {
                    Logger.Error("???", ex);
                    MessageBoxVM.Show(new(ex.ToString()) { Icon = MessageBoxVM.Icon.Error });
                }
            }
        }

        private ListBoxItemVM CreateFactionItem(string faction, string factionPath)
        {
            return new()
            {
                Name = faction,
                Content = GetVanillaFactionI18n(faction),
                ToolTip = factionPath,
                Tag = this,
            };
        }

        private ListBoxItemVM CreatePortraitItem(
            Gender gender,
            string faction,
            string portraitName,
            string portraitPath,
            Stream imageStream
        )
        {
            return new()
            {
                Name = portraitName,
                ToolTip = portraitPath,
                Tag = imageStream,
                ContextMenu = CreatePortraitContextMenu(faction, gender),
            };
        }

        #region CreatePortraitContextMenu
        private ContextMenuVM CreatePortraitContextMenu(string faction, Gender gender)
        {
            return new(
                (list) =>
                {
                    list.Add(OpenPortraitMenuItem());
                    list.Add(OpenDirectoryAndLocatePortraitMenuItem());
                    list.Add(RemoveSelectedPortraitsMenuItem(gender));
                    list.Add(DeleteSelectedPortraitsMenuItem(gender));
                }
            );
            MenuItemVM OpenPortraitMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "打开肖像";
                menuItem.CommandEvent += (o) =>
                {
                    if (o is not ListBoxItemVM item)
                        return;
                    Utils.OpenLink(Path.Combine(BaseDirectory, item.ToolTip!.ToString()!));
                };
                return menuItem;
            }
            MenuItemVM OpenDirectoryAndLocatePortraitMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "打开肖像位置";
                menuItem.CommandEvent += (o) =>
                {
                    if (o is not ListBoxItemVM item)
                        return;
                    Utils.OpenDirectoryAndLocateFile(
                        Path.Combine(BaseDirectory, item.ToolTip!.ToString()!)
                    );
                };
                return menuItem;
            }
            MenuItemVM RemoveSelectedPortraitsMenuItem(Gender gender)
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "删除选中肖像";
                menuItem.CommandEvent += (o) =>
                {
                    if (o is not ListBoxItemVM item)
                        return;
                    RemoveSelectedPortraits(faction, gender);
                };
                return menuItem;
            }
            MenuItemVM DeleteSelectedPortraitsMenuItem(Gender gender)
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "卸载选中肖像";
                menuItem.CommandEvent += (o) =>
                {
                    if (o is not ListBoxItemVM item)
                        return;
                    DeleteSelectedPortraits(faction, gender);
                };
                return menuItem;
            }
        }

        #region TryRemoveSelectedPortraits

        private void RemoveSelectedPortraits(string faction, Gender gender)
        {
            List<ListBoxItemVM> selectedPortraitItems;
            ObservableCollection<ListBoxItemVM> nowShowPortraitItems;
            if (gender is Gender.Male)
            {
                selectedPortraitItems = PortraitsManagerViewModel.Instance.NowSelectedMalePortraitItems;
                nowShowPortraitItems = PortraitsManagerViewModel.Instance.NowShowMalePortraitItems;
            }
            else
            {
                selectedPortraitItems = PortraitsManagerViewModel.Instance.NowSelectedFemalePortraitItems;
                nowShowPortraitItems = PortraitsManagerViewModel.Instance.NowShowFemalePortraitItems;
            }
            var count = selectedPortraitItems.Count;
            for (int i = 0; i < count; i++)
            {
                var factionPortrait = _allFactionPortraits[faction];
                var item = selectedPortraitItems[i];
                var portraitPath = item.ToolTip!.ToString()!;
                var useStatus = GetPortraitUseStatus(portraitPath);
                if (useStatus.Count is 1 && useStatus.First().Value is not Gender.All)
                {
                    if (
                        MessageBoxVM.Show(
                            new($"此肖像 {portraitPath} 为分组 {GroupId} 唯一引用的位置\n解除引用将删除文件, 你确定吗?")
                            {
                                Button = MessageBoxVM.Button.YesNo,
                                Icon = MessageBoxVM.Icon.Question
                            }
                        )
                        is not MessageBoxVM.Result.Yes
                    )
                        return;
                    _planToDeletePortraitPaths.Add(portraitPath);
                }
                nowShowPortraitItems.Remove(item);
                factionPortrait.Remove(portraitPath, gender);
            }
            PortraitsManagerViewModel.Instance.IsRemindSave = true;
        }
        #endregion

        #region TryDeleteSelectedPortraits
        private void DeleteSelectedPortraits(string faction, Gender gender)
        {
            var selectedPortraitItems =
                gender is Gender.Male
                    ? PortraitsManagerViewModel.Instance.NowSelectedMalePortraitItems
                    : PortraitsManagerViewModel.Instance.NowSelectedFemalePortraitItems;
            var count = selectedPortraitItems.Count;
            if (
                MessageBoxVM.Show(
                    new(
                        $"此操作将解除引用并删除以下文件, 你确定吗?\n{string.Join("\n", selectedPortraitItems.Select(i => i.ToolTip))}"
                    )
                    {
                        Icon = MessageBoxVM.Icon.Question,
                        Button = MessageBoxVM.Button.YesNo
                    }
                )
                is not MessageBoxVM.Result.Yes
            )
                return;
            for (int i = 0; i < count; i++)
            {
                var item = selectedPortraitItems[i];
                var portraitPath = item.ToolTip!.ToString()!;
                UnreferenceAllPortrait(portraitPath);
                _planToDeletePortraitPaths.Add(portraitPath);
            }
            PortraitsManagerViewModel.Instance.IsRemindSave = true;
        }

        private void UnreferenceAllPortrait(string portraitPath)
        {
            foreach (var factionPortrait in _allFactionPortraits)
            {
                factionPortrait.Value.Remove(portraitPath);
                var maleFactionPortraitItem = MaleFactionPortraitsItem[factionPortrait.Value.FactionId];
                if (maleFactionPortraitItem.FirstOrDefault(i => i?.ToolTip?.ToString() == portraitPath, null) is ListBoxItemVM maleItem)
                    maleFactionPortraitItem.Remove(maleItem);
                var femaleFactionPortraitItem = FemaleFactionPortraitsItem[factionPortrait.Value.FactionId];
                if (femaleFactionPortraitItem.FirstOrDefault(i => i?.ToolTip?.ToString() == portraitPath, null) is ListBoxItemVM femaleItem)
                    femaleFactionPortraitItem.Remove(femaleItem);
            }
        }

        #endregion

        private Dictionary<string, Gender> GetPortraitUseStatus(string portraitPath)
        {
            var useStatus = new Dictionary<string, Gender>();
            foreach (var factionPortrait in _allFactionPortraits)
            {
                Gender? gender = null;
                if (factionPortrait.Value.MalePortraitsPath.Contains(portraitPath))
                    gender = Gender.Male;
                if (factionPortrait.Value.FemalePortraitsPath.Contains(portraitPath))
                    gender = gender is null ? Gender.Female : Gender.All;
                if (gender is not null)
                    useStatus.TryAdd(factionPortrait.Key, (Gender)gender);
            }
            return useStatus;
        }
        #endregion

        #region AddPortrait
        public void TryAddPortrait(IEnumerable<string> pathList, string faction, Gender gender)
        {
            string commonPath = Path.GetFullPath(Path.Combine(pathList.Select(Path.GetDirectoryName).ToArray()!)) + "\\";
            foreach (var path in pathList)
            {
                if (Directory.Exists(path) && Utils.GetAllSubFiles(path) is List<FileInfo> fileList)
                {
                    foreach (var file in fileList)
                        AddPortraitFile(commonPath, file.FullName, faction, gender);
                    continue;
                }
                AddPortraitFile(commonPath, path, faction, gender);

            }
        }
        public List<FileInfo> AddPortraitDirectory(string directory) =>
            Utils.GetAllSubFiles(directory);
        public void AddPortraitFile(string commonPath, string file, string faction, Gender gender)
        {
            var filePath = file.Replace(commonPath, "");
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var pmPortraitsPath = Path.Combine(PMPortraitsDirectoryPath, filePath);
            var pmPortraitsFullName = Path.Combine(PMPortraitsDirectory, filePath);
            var pmPortraitsDirectory = Path.GetDirectoryName(pmPortraitsFullName);
            if (!Directory.Exists(pmPortraitsDirectory))
                Directory.CreateDirectory(pmPortraitsDirectory!);
            File.Copy(file, pmPortraitsFullName, true);
            var factionPortrait = _allFactionPortraits[faction];
            var stream = new StreamReader(pmPortraitsFullName).BaseStream;
            _allImageStream.Add(pmPortraitsPath, stream);
            factionPortrait.Add(pmPortraitsPath, gender);
            var portraitItem = CreatePortraitItem(gender, faction, fileName, pmPortraitsPath, stream);
            if (gender is Gender.Male)
                MaleFactionPortraitsItem[faction].Add(portraitItem);
            else
                FemaleFactionPortraitsItem[faction].Add(portraitItem);
        }
        #endregion
        #region Save
        public void Save()
        {
            SaveAllFactionPortrait();
            DeletePlanToDeleteFiles();
        }

        private void SaveAllFactionPortrait()
        {
            foreach (var factionPortrait in _allFactionPortraits)
            {
                if (factionPortrait.Value.IsChanged)
                {
                    CheckBackupDirectory();
                    BackupFactionPortraitData(factionPortrait.Value);
                    factionPortrait.Value.Save();
                }
            }
        }

        private void DeletePlanToDeleteFiles()
        {
            foreach (var portraitPath in _planToDeletePortraitPaths)
            {
                _allImageStream[portraitPath].Close();
                _allImageStream.Remove(portraitPath);
                var portraitFile = Path.Combine(BaseDirectory, portraitPath);
                if (!TryDeletePortraitDirectory(portraitFile))
                    Utils.DeleteFileToRecycleBin(portraitFile);
            }
            _planToDeletePortraitPaths.Clear();
        }

        private bool TryDeletePortraitDirectory(string portraitFile)
        {
            var portraitDirectory = Path.GetDirectoryName(portraitFile)!;
            if (portraitDirectory.EndsWith(PMPortraitsDirectoryPath))
                return false;
            if (Directory.GetFiles(portraitDirectory)?.Length is 1)
            {
                Utils.DeleteDirToRecycleBin(portraitDirectory);
                return true;
            }
            return false;
        }

        private void CheckBackupDirectory()
        {
            if (!Directory.Exists(PMDirectory))
                Directory.CreateDirectory(PMDirectory);
            if (!Directory.Exists(PMBackupDirectory))
                Directory.CreateDirectory(PMBackupDirectory);
        }

        private void BackupFactionPortraitData(FactionPortrait factionPortrait)
        {
            if (
                FactionPortrait.TryGetFactionPortraitData(factionPortrait.FileFullName)
                is not string factionPortraitData
            )
                throw new("你有问题");
            File.WriteAllText(
                Path.Combine(PMBackupDirectory, factionPortrait.FileName),
                factionPortraitData
            );
        }
        #endregion
        #region Close
        public void Close()
        {
            foreach (var kv in _allImageStream)
                kv.Value.Close();
        }
        #endregion
        private string GetVanillaFactionI18n(string factionId) =>
            VanillaFactions.AllVanillaFactionsI18n.TryGetValue(factionId, out var name)
                ? name
                : factionId;
    }
}
