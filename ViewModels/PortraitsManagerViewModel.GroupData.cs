using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
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

        partial void OnHeaderChanged(string vaule)
        {
            _header = $"{vaule} ({_allImageStream.Count})";
        }

        [ObservableProperty]
        private string _toolTip = string.Empty;

        [ObservableProperty]
        private bool _isExpanded = false;

        [ObservableProperty]
        private ListBoxVM _factionList = new();

        [ObservableProperty]
        private ContextMenuVM _contextMenu = new();

        [ObservableProperty]
        private Dictionary<string, ObservableCollection<ListBoxItemVM>> _maleFactionPortraitsItem =
            new();

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
            ToolTip = groupName;
            GroupId = groupId;
            BaseDirectory = baseDirectory;
            FactionDirectory = $"{baseDirectory}\\data\\world\\factions";
            PMDirectory = $"{baseDirectory}\\{nameof(StarsectorToolsExtension)}.PortraitsManager";
            PMPortraitsDirectory = $"{PMDirectory}\\Portraits";
            PMPortraitsDirectoryPath =
                $"{nameof(StarsectorToolsExtension)}.PortraitsManager\\Portraits";
            PMBackupDirectory = $"{PMDirectory}\\Backup";
            PMFactionBackupDirectory = $"{PMBackupDirectory}\\Faction";
            ParseBaseDirectory(BaseDirectory, FactionDirectory);
            SetGroupDataContextMenu();
            RefreshDispalyData(false);
        }

        private void SetGroupDataContextMenu()
        {
            ContextMenu = new(
                (list) =>
                {
                    list.Add(AddFactionMenuItem());
                }
            );
            MenuItemVM AddFactionMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "添加势力";
                menuItem.CommandEvent += (o) =>
                {
                    //if (GroupId is PortraitsManagerViewModel._StrVanilla)
                    //{
                    //    MessageBoxVM.Show(new("无法在原版添加新势力"));
                    //    return;
                    //}
                    //FactionPortrait.CreateTo();
                    //TryGetFactionPortrait
                };
                return menuItem;
            }
        }
        private void RefreshDispalyData(bool isRemindSave = true)
        {
            RefreshHeader();
            RefreshFactionItems();
            PortraitsManagerViewModel.Instance.IsRemindSave = isRemindSave;
        }

        private void RefreshHeader()
        {
            Header = ToolTip;
        }

        private void RefreshFactionItems()
        {
            foreach (var item in FactionList)
            {
                var factionId = item.Id!;
                var factionName = item.Name!;
                item.Content =
                    $"{factionName} ({MaleFactionPortraitsItem[factionId].Count},{FemaleFactionPortraitsItem[factionId].Count})";
            }
        }

        public static GroupData? Create(string groupId, string groupName, string baseDirectory)
        {
            var temp = new GroupData(groupId, groupName, baseDirectory);
            if (temp._allFactionPortraits.Any())
                return temp;
            return null;
        }

        #region ParseBaseDirectory
        private void ParseBaseDirectory(string baseDirectory, string factionDirectory)
        {
            if (Utils.GetAllSubFiles(factionDirectory) is not List<FileInfo> fileList)
                return;
            TryGetFactionPortraits(baseDirectory, fileList);
        }

        private void TryGetFactionPortraits(string baseDirectory, IList<FileInfo> fileList)
        {
            foreach (var file in fileList)
            {
                try
                {
                    TryGetFactionPortrait(baseDirectory, file.FullName);
                }
                catch (Exception ex)
                {
                    Logger.Error("???", ex);
                    MessageBoxVM.Show(new(ex.ToString()) { Icon = MessageBoxVM.Icon.Error });
                }
            }
        }

        private void TryGetFactionPortrait(string baseDirectory, string file)
        {
            if (
                FactionPortrait.Create(file, baseDirectory, out var errMessage)
                is not FactionPortrait factionPortrait
            )
                return;
            var faction = Path.GetFileNameWithoutExtension(file);
            FactionList.Add(CreateFactionItem(faction, file));
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
        #endregion
        #region CreateFactionItem
        private ListBoxItemVM CreateFactionItem(string faction, string factionFile)
        {
            return new()
            {
                Id = faction,
                Name = GetVanillaFactionI18n(faction),
                Content = GetVanillaFactionI18n(faction),
                ToolTip = factionFile,
                Tag = this,
                ContextMenu = CreateFactionItemContextMenu(faction, factionFile),
            };
        }

        private ContextMenuVM CreateFactionItemContextMenu(string faction, string factionFile)
        {
            return new(
                (list) =>
                {
                    list.Add(OpenDirectoryMenuItem());
                    list.Add(ClearPortraits());
                }
            );
            MenuItemVM OpenDirectoryMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "打开文件位置";
                menuItem.CommandEvent += (o) =>
                {
                    if (o is not ListBoxItemVM item)
                        return;
                    Utils.OpenLink(Path.Combine(BaseDirectory, item.ToolTip!.ToString()!));
                };
                return menuItem;
            }
            MenuItemVM ClearPortraits()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "清除肖像";
                menuItem.ItemsSource = new();
                menuItem.Add(ClearAllPortraits());
                menuItem.Add(ClearMalePortraits());
                menuItem.Add(ClearFemalePortraits());
                return menuItem;
                MenuItemVM ClearAllPortraits()
                {
                    var menuItem = new MenuItemVM();
                    menuItem.Header = "清除所有肖像";
                    menuItem.CommandEvent += (o) =>
                    {
                        if (o is not ListBoxItemVM item)
                            return;
                        var faction = item.Id!.ToString()!;
                        ClearPortraits(faction, Gender.Male);
                        ClearPortraits(faction, Gender.Female);
                        RefreshDispalyData();
                    };
                    return menuItem;
                }
                MenuItemVM ClearMalePortraits()
                {
                    var menuItem = new MenuItemVM();
                    menuItem.Header = "清除男性肖像";
                    menuItem.CommandEvent += (o) =>
                    {
                        if (o is not ListBoxItemVM item)
                            return;
                        var faction = item.Id!.ToString()!;
                        ClearPortraits(faction, Gender.Male);
                        RefreshDispalyData();
                    };
                    return menuItem;
                }
                MenuItemVM ClearFemalePortraits()
                {
                    var menuItem = new MenuItemVM();
                    menuItem.Header = "清除女性肖像";
                    menuItem.CommandEvent += (o) =>
                    {
                        if (o is not ListBoxItemVM item)
                            return;
                        var faction = item.Id!.ToString()!;
                        ClearPortraits(faction, Gender.Female);
                        RefreshDispalyData();
                    };
                    return menuItem;
                }
                void ClearPortraits(string faction, Gender gender)
                {
                    IList<ListBoxItemVM> nowSelectedPortraitItems;
                    if (gender is Gender.Male)
                        nowSelectedPortraitItems = PortraitsManagerViewModel
                            .Instance
                            .NowShowMalePortraitItems;
                    else
                        nowSelectedPortraitItems = PortraitsManagerViewModel
                            .Instance
                            .NowShowFemalePortraitItems;
                    RemovePortraits(
                        nowSelectedPortraitItems,
                        nowSelectedPortraitItems,
                        faction,
                        gender
                    );
                }
            }
        }
        #endregion
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
                selectedPortraitItems = PortraitsManagerViewModel
                    .Instance
                    .NowSelectedMalePortraitItems;
                nowShowPortraitItems = PortraitsManagerViewModel.Instance.NowShowMalePortraitItems;
            }
            else
            {
                selectedPortraitItems = PortraitsManagerViewModel
                    .Instance
                    .NowSelectedFemalePortraitItems;
                nowShowPortraitItems = PortraitsManagerViewModel
                    .Instance
                    .NowShowFemalePortraitItems;
            }
            RemovePortraits(nowShowPortraitItems, selectedPortraitItems, faction, gender);
            RefreshDispalyData();
        }

        private void RemovePortraits(
            IList<ListBoxItemVM> showItems,
            IList<ListBoxItemVM> removeItems,
            string faction,
            Gender gender
        )
        {
            var autoDelete = false;
            var count = removeItems.Count;
            for (int i = 0; i < count; i++)
            {
                var factionPortrait = _allFactionPortraits[faction];
                var item = removeItems[i];
                var portraitPath = item.ToolTip!.ToString()!;
                var useStatus = GetPortraitUseStatus(portraitPath);
                if (useStatus.Count is 1 && useStatus.First().Value is not Gender.All)
                {
                    MessageBoxVM.Result? result;
                    if (!autoDelete)
                    {
                        result = MessageBoxVM.Show(
                            new(
                                $"以下肖像为分组 {ToolTip} 唯一引用的位置. 解除引用将删除文件, 请选择操作:"
                                    + $"\n取消: 取消删除此文件  否: 只删除此文件  确定: 删除并对接下来的唯一引用文件执行相同的操作"
                                    + $"\n{portraitPath}"
                            )
                            {
                                Button = MessageBoxVM.Button.YesNoCancel,
                                Icon = MessageBoxVM.Icon.Question
                            }
                        );
                        if (result is MessageBoxVM.Result.Cancel)
                            continue;
                        else if (result is MessageBoxVM.Result.Yes)
                            autoDelete = true;
                    }
                    _planToDeletePortraitPaths.Add(portraitPath);
                }
                showItems.Remove(item);
                factionPortrait.Remove(portraitPath, gender);
            }
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
            RefreshDispalyData();
        }

        private void UnreferenceAllPortrait(string portraitPath)
        {
            foreach (var factionPortrait in _allFactionPortraits)
            {
                factionPortrait.Value.Remove(portraitPath);
                var maleFactionPortraitItem = MaleFactionPortraitsItem[
                    factionPortrait.Value.FactionId
                ];
                if (
                    maleFactionPortraitItem.FirstOrDefault(
                        i => i?.ToolTip?.ToString() == portraitPath,
                        null
                    )
                    is ListBoxItemVM maleItem
                )
                    maleFactionPortraitItem.Remove(maleItem);
                var femaleFactionPortraitItem = FemaleFactionPortraitsItem[
                    factionPortrait.Value.FactionId
                ];
                if (
                    femaleFactionPortraitItem.FirstOrDefault(
                        i => i?.ToolTip?.ToString() == portraitPath,
                        null
                    )
                    is ListBoxItemVM femaleItem
                )
                    femaleFactionPortraitItem.Remove(femaleItem);
            }
        }

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
        #endregion
        #region TryAddPortrait
        public void TryAddPortrait(IEnumerable<string> pathList, string faction, Gender gender)
        {
            StringBuilder errSB = new();
            string commonPath =
                Path.GetFullPath(Path.Combine(pathList.Select(Path.GetDirectoryName).ToArray()!))
                + "\\";
            foreach (var path in pathList)
            {
                if (Directory.Exists(path) && Utils.GetAllSubFiles(path) is List<FileInfo> fileList)
                {
                    foreach (var file in fileList)
                    {
                        if (!AddPortraitFile(commonPath, file.FullName, faction, gender))
                            errSB.AppendLine(file.FullName);
                    }
                    continue;
                }
                if (!AddPortraitFile(commonPath, path, faction, gender))
                    errSB.AppendLine(path);
            }
            RefreshDispalyData();
            if (errSB.Length > 0)
                MessageBoxVM.Show(new($"以下文件添加失败 必须是128*128的PNG文件\n{errSB}"));
        }

        private List<FileInfo> AddPortraitDirectory(string directory) =>
            Utils.GetAllSubFiles(directory);

        private bool AddPortraitFile(string commonPath, string file, string faction, Gender gender)
        {
            if (!CheckFileIsImage(file))
                return false;
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
            var portraitItem = CreatePortraitItem(
                gender,
                faction,
                fileName,
                pmPortraitsPath,
                stream
            );
            if (gender is Gender.Male)
                MaleFactionPortraitsItem[faction].Add(portraitItem);
            else
                FemaleFactionPortraitsItem[faction].Add(portraitItem);
            return true;
        }

        private bool CheckFileIsImage(string file)
        {
            using FileStream fs = new(file, FileMode.Open);
            byte[] head = new byte[4];
            fs.Read(head);
            var pngHead = BitConverter.ToUInt32(head.Reverse().ToArray());
            if (pngHead is 0x89504E47)
            {
                fs.Seek(16, SeekOrigin.Begin);
                fs.Read(head);
                int width = BitConverter.ToInt32(head.Reverse().ToArray());
                fs.Read(head);
                int height = BitConverter.ToInt32(head.Reverse().ToArray());
                fs.Close();
                if (width is 128 && height is 128)
                    return true;
            }
            return false;
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
                if (!factionPortrait.Value.IsChanged)
                    continue;
                CheckBackupDirectory();
                BackupFactionPortraitData(factionPortrait.Value);
                factionPortrait.Value.Save();
            }
        }

        private void DeletePlanToDeleteFiles()
        {
            // 通过排序来优先删除子文件夹的内容
            foreach (var portraitPath in _planToDeletePortraitPaths.OrderBy(s => s))
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
