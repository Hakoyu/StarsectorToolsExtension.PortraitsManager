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

        public bool IsChangeed { get; private set; }

        /// <summary>根目录</summary>
        private readonly string _BaseDirectory;

        /// <summary>势力目录</summary>
        private readonly string _FactionsDirectory;

        /// <summary>肖像目录</summary>
        private readonly string _PortraitsDirectory;

        /// <summary>拓展根目录</summary>
        private readonly string _PMDirectory;

        /// <summary>肖像目录</summary>
        private readonly string _PMPortraitsDirectory;

        /// <summary>肖像相对目录</summary>
        private readonly string _PMPortraitsDirectoryPath;

        /// <summary>备份目录</summary>
        private readonly string _PMBackupDirectory;

        /// <summary>备份文件</summary>
        private readonly string _PMBackupZIPFile;

        /// <summary>势力备份目录</summary>
        private readonly string _PMFactionsBackupDirectory;

        /// <summary>肖像备份目录</summary>
        private readonly string _PMPortraitsBackupDirectory;

        /// <summary>临时肖像备份目录</summary>
        private readonly string _PMTempBackupDirectory;

        /// <summary>临时肖像备份目录</summary>
        private readonly string _PMTempFactionsBackupDirectory;

        /// <summary>临时肖像备份目录</summary>
        private readonly string _PMTempPortraitsBackupDirectory;

        [ObservableProperty]
        private string _header = string.Empty;

        partial void OnHeaderChanged(string vaule)
        {
            _header = $"{vaule} ({_allFactionPortraits.Count},{_allImageStream.Count})";
        }

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private string _toolTip = string.Empty;

        [ObservableProperty]
        private bool _isExpanded = false;

        [ObservableProperty]
        private ListBoxVM _factionList = new();

        [ObservableProperty]
        private ContextMenuVM _contextMenu = new();

        [ObservableProperty]
        private Dictionary<string, ObservableCollection<ListBoxItemVM>> _maleFactionPortraitItems =
            new();

        [ObservableProperty]
        private Dictionary<
            string,
            ObservableCollection<ListBoxItemVM>
        > _femaleFactionPortraitItems = new();

        private readonly Dictionary<string, FactionPortrait> _allFactionPortraits = new();
        private readonly Dictionary<string, Stream> _allImageStream = new();

        private readonly HashSet<string> _planToDeleteFaction = new();
        private readonly HashSet<string> _planToDeletePortraitPaths = new();

        private GroupData(string groupId, string groupName, string baseDirectory)
        {
            ToolTip = groupName;
            GroupId = groupId;

            _BaseDirectory = baseDirectory;
            _FactionsDirectory = $"{_BaseDirectory}\\data\\world\\factions";
            _PortraitsDirectory = $"{_BaseDirectory}\\graphics\\portraits";
            _PMDirectory = $"{_BaseDirectory}\\{nameof(StarsectorToolsExtension)}.PortraitsManager";
            _PMPortraitsDirectory = $"{_PMDirectory}\\Portraits";
            _PMPortraitsDirectoryPath =
                $"{nameof(StarsectorToolsExtension)}.PortraitsManager\\Portraits";
            _PMBackupDirectory = $"{_PMDirectory}\\Backup";
            _PMBackupZIPFile = $"{_PMBackupDirectory}\\Backup.zip";
            _PMFactionsBackupDirectory = $"{_PMBackupDirectory}\\Faction";
            _PMPortraitsBackupDirectory = $"{_PMBackupDirectory}\\Portraits";
            _PMTempBackupDirectory = $"{_PMBackupDirectory}\\Backup";
            _PMTempFactionsBackupDirectory = $"{_PMBackupDirectory}\\Backup\\Factions";
            _PMTempPortraitsBackupDirectory = $"{_PMBackupDirectory}\\Backup\\Portraits";

            InitializeData();
        }

        private void InitializeData()
        {
            ParseBaseDirectory(_BaseDirectory, _FactionsDirectory);
            SetGroupDataContextMenu();
            RefreshDispalyData(false);
        }

        private void SetGroupDataContextMenu()
        {
            ContextMenu = new(
                (list) =>
                {
                    list.Add(OpenDirectoryMenuItem());
                    list.Add(AddFactionMenuItem());
                    list.Add(CleanAllFactionPortraitsMenuItem());
                    list.Add(BackupMenuItem());
                    list.Add(RestoreBackupMenuItem());
                }
            );
            MenuItemVM OpenDirectoryMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "打开文件夹";
                menuItem.ItemsSource = new();
                menuItem.Add(OpenBaseDirectoryMenuItem());
                menuItem.Add(OpenPMDirectoryMenuItem());
                menuItem.CommandEvent += (o) =>
                {
                    Utils.OpenLink(_BaseDirectory);
                };
                return menuItem;
                MenuItemVM OpenBaseDirectoryMenuItem()
                {
                    var menuItem = new MenuItemVM();
                    menuItem.Header = "打开模组文件夹";
                    menuItem.CommandEvent += (o) =>
                    {
                        Utils.OpenLink(_BaseDirectory);
                    };
                    return menuItem;
                }
                MenuItemVM OpenPMDirectoryMenuItem()
                {
                    var menuItem = new MenuItemVM();
                    menuItem.Header = "打开肖像管理器文件夹";
                    menuItem.CommandEvent += (o) =>
                    {
                        if (!Directory.Exists(_PMDirectory))
                        {
                            MessageBoxVM.Show(new("肖像管理器文件夹不存在"));
                            return;
                        }
                        Utils.OpenLink(_PMDirectory);
                    };
                    return menuItem;
                }
            }
            MenuItemVM AddFactionMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "添加势力";
                menuItem.CommandEvent += (o) =>
                {
                    if (GroupId is PortraitsManagerViewModel._StrVanilla)
                    {
                        MessageBoxVM.Show(new("无法在原版添加新势力"));
                        return;
                    }
                    var viewModel = PortraitsManagerViewModel.Instance.AddFactionWindowViewModel;
                    viewModel.BaseGroupData = this;
                    viewModel.ShowDialog();
                };
                return menuItem;
            }
            MenuItemVM CleanAllFactionPortraitsMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "清空所有肖像";
                menuItem.CommandEvent += (o) =>
                {
                    if (
                        MessageBoxVM.Show(
                            new("此操作将清空模组内使用的所有肖像(以及肖像文件),你确定吗")
                            {
                                Icon = MessageBoxVM.Icon.Question,
                                Button = MessageBoxVM.Button.YesNo
                            }
                        )
                        is not MessageBoxVM.Result.Yes
                    )
                        return;
                    DeleteAllPortraits();
                    RefreshDispalyData();
                };
                return menuItem;
            }
            MenuItemVM BackupMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "备份至文件";
                menuItem.CommandEvent += async (o) =>
                {
                    if (PortraitsManagerViewModel.Instance.IsRemindSave)
                    {
                        if (
                            MessageBoxVM.Show(
                                new("当前数据未保存,确定要保存吗")
                                {
                                    Icon = MessageBoxVM.Icon.Question,
                                    Button = MessageBoxVM.Button.YesNo
                                }
                            ) is MessageBoxVM.Result.Yes
                        )
                            PortraitsManagerViewModel.Instance.Save();
                    }
                    CreateBackupDirectory();
                    if (
                        SaveFileDialogVM.Show(
                            new()
                            {
                                Title = "选择保存的文件夹",
                                Filter = $"Zip 文件|*.zip",
                                FileName = $"{GroupId}.zip",
                                InitialDirectory = _PMBackupDirectory
                            }
                        )
                            is not string file
                        || string.IsNullOrWhiteSpace(file)
                    )
                        return;
                    if (!file.EndsWith(".zip"))
                        file += ".zip";
                    await BackupToArchiveFile(Path.GetDirectoryName(file)!);
                };
                return menuItem;
            }
            MenuItemVM RestoreBackupMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "从备份文件还原";
                menuItem.CommandEvent += async (o) =>
                {
                    CreateBackupDirectory();
                    var fileNames = OpenFileDialogVM.Show(
                        new()
                        {
                            Title = "选择备份文件",
                            Filter = $"Zip 文件|*.zip",
                            InitialDirectory = _PMBackupDirectory
                        }
                    );
                    if (fileNames?.FirstOrDefault(defaultValue: null) is not string fileName)
                        return;
                    CreateTempBackupDirectory();
                    if (
                        await Utils.UnArchiveFileToDirectory(fileName, _PMTempBackupDirectory)
                        is false
                    )
                    {
                        MessageBoxVM.Show(new("文件解压错误"));
                        return;
                    }
                    if (
                        Utils.GetAllSubFiles(_PMTempFactionsBackupDirectory)
                        is not List<FileInfo> fileList
                    )
                        return;
                    // 从文件读取备份的势力肖像信息
                    foreach (var file in fileList)
                    {
                        var portraitData = FactionPortrait.TryGetFactionPortraitData(
                            file.FullName
                        )!;
                        var factionFile = Path.Combine(_FactionsDirectory, file.Name);
                        // 如果存在势力文件,则替换,否则创建
                        if (File.Exists(factionFile))
                            FactionPortrait.ReplaceTo(factionFile, portraitData);
                        else
                            FactionPortrait.SaveTo(factionFile, portraitData);
                    }
                    // 清空所有数据
                    Close();
                    // 获取上层文件夹名
                    var directoryName = Path.GetFileName(_BaseDirectory);
                    var portraitDirectory = Path.Combine(_PMTempBackupDirectory, directoryName);
                    // 移动至新文件夹(重命名至商城文件夹名,让接下来的移动操作变成替换)
                    Directory.Move(_PMTempPortraitsBackupDirectory, portraitDirectory);
                    // 移动肖像已替换原始文件
                    Utils.MoveDirectory(portraitDirectory, Path.GetDirectoryName(_BaseDirectory)!);
                    // 初始化数据
                    InitializeData();
                    DeleteTempBackupDirectory();
                    PortraitsManagerViewModel.Instance.CleanShowPortraitItems();
                    // TODO: 还原备份
                };
                return menuItem;
            }
        }

        private void RestoreFactionBackup(string faction, string file)
        {
            var factionPortrait = FactionPortrait.Create(file, _BaseDirectory, out _)!;
            _allFactionPortraits[faction] = factionPortrait;
            var maleCollection = new ObservableCollection<ListBoxItemVM>();
            var femaleCollection = new ObservableCollection<ListBoxItemVM>();
            foreach (var portraitPath in factionPortrait.AllPortraitsPath)
                GetFactionPortrait(
                    faction,
                    portraitPath,
                    factionPortrait,
                    maleCollection,
                    femaleCollection
                );
            MaleFactionPortraitItems[faction] = maleCollection;
            FemaleFactionPortraitItems[faction] = femaleCollection;
        }

        public bool TryAddFaction(string faction)
        {
            if (_allFactionPortraits.ContainsKey(faction))
            {
                MessageBoxVM.Show(new("势力已存在") { ShowMainWindowBlurEffect = false });
                return false;
            }
            AddFaction(faction);
            return true;
        }

        private void AddFaction(string faction)
        {
            var file = FactionPortrait.CombineFactionPath(_FactionsDirectory, faction);
            FactionPortrait.CreateTo(file);
            _planToDeleteFaction.Remove(faction);
            TryGetFactionPortrait(_BaseDirectory, file);
            RefreshDispalyData();
        }

        public bool TryRenameFaction(string faction, string newFaction)
        {
            if (_allFactionPortraits.ContainsKey(newFaction))
            {
                MessageBoxVM.Show(new("势力已存在") { ShowMainWindowBlurEffect = false });
                return false;
            }
            RenameFaction(faction, newFaction);
            return true;
        }

        private void RenameFaction(string faction, string newFaction)
        {
            var item = FactionList.First(i => i.Id == faction);
            var file = FactionPortrait.CombineFactionPath(_FactionsDirectory, faction);
            var newFactionFile = FactionPortrait.CombineFactionPath(_FactionsDirectory, newFaction);
            File.WriteAllText(newFactionFile, File.ReadAllText(file));
            Utils.DeleteFileToRecycleBin(file);
            // 重命名势力肖像集
            var factionPortraits = _allFactionPortraits[faction];
            factionPortraits.Rename(newFaction);
            _allFactionPortraits.Remove(faction);
            _allFactionPortraits.Add(newFaction, factionPortraits);
            // 重命名显示项
            InitializeFactionItemData(item, newFaction, newFactionFile);
            // 重命名肖像列表
            var maleItems = MaleFactionPortraitItems[faction];
            MaleFactionPortraitItems.Remove(faction);
            MaleFactionPortraitItems.Add(newFaction, maleItems);
            var femaleItems = FemaleFactionPortraitItems[faction];
            FemaleFactionPortraitItems.Remove(faction);
            FemaleFactionPortraitItems.Add(newFaction, femaleItems);

            _planToDeleteFaction.Remove(faction);
            _planToDeleteFaction.Remove(newFaction);
            RefreshDispalyData();
        }

        public bool TryRemoveFaction(string faction)
        {
            RemoveFaction(faction);
            return true;
        }

        private void RemoveFaction(string faction)
        {
            if (!_allFactionPortraits.ContainsKey(faction))
            {
                MessageBoxVM.Show(new("势力不存在") { ShowMainWindowBlurEffect = false });
                return;
            }
            if (
                MessageBoxVM.Show(
                    new("此操作会删除势力并解除其引用的文件,你确定吗?")
                    {
                        Icon = MessageBoxVM.Icon.Question,
                        Button = MessageBoxVM.Button.YesNo
                    }
                )
                is not MessageBoxVM.Result.Yes
            )
                return;
            IList<ListBoxItemVM> nowSelectedPortraitItems;
            // 清除包含的男性肖像
            nowSelectedPortraitItems = PortraitsManagerViewModel.Instance.NowShowMalePortraitItems;
            RemovePortraits(
                nowSelectedPortraitItems,
                nowSelectedPortraitItems,
                faction,
                Gender.Male
            );
            // 清除包含的女性肖像
            nowSelectedPortraitItems = PortraitsManagerViewModel
                .Instance
                .NowShowFemalePortraitItems;
            RemovePortraits(
                nowSelectedPortraitItems,
                nowSelectedPortraitItems,
                faction,
                Gender.Female
            );

            var file = _allFactionPortraits[faction].FileFullName;
            _planToDeleteFaction.Add(faction);
            FactionList.Remove(FactionList.First(i => i.Id == faction));
            _allFactionPortraits.Remove(faction);
            MaleFactionPortraitItems.Remove(faction);
            FemaleFactionPortraitItems.Remove(faction);
            RefreshDispalyData();
        }

        private void RefreshDispalyData(bool isRemindSave = true)
        {
            RefreshHeader();
            RefreshFactionItems();
            IsChangeed = isRemindSave;
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
                    $"{factionName} ({MaleFactionPortraitItems[factionId].Count},{FemaleFactionPortraitItems[factionId].Count})";
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
            TryAllGetFactionPortraits(baseDirectory, fileList);
        }

        private void TryAllGetFactionPortraits(string baseDirectory, IList<FileInfo> fileList)
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
                FactionPortrait.Create(file, baseDirectory, out _)
                is not FactionPortrait factionPortrait
            )
                return;
            var faction = Path.GetFileNameWithoutExtension(file);
            FactionList.Add(CreateFactionItem(faction, file));
            var maleCollection = new ObservableCollection<ListBoxItemVM>();
            var femaleCollection = new ObservableCollection<ListBoxItemVM>();
            foreach (var portraitPath in factionPortrait.AllPortraitsPath)
                GetFactionPortrait(
                    faction,
                    portraitPath,
                    factionPortrait,
                    maleCollection,
                    femaleCollection
                );
            _allFactionPortraits.Add(faction, factionPortrait);
            MaleFactionPortraitItems.Add(faction, maleCollection);
            FemaleFactionPortraitItems.Add(faction, femaleCollection);
        }

        private void GetFactionPortrait(
            string faction,
            string portraitPath,
            FactionPortrait factionPortrait,
            ObservableCollection<ListBoxItemVM> maleCollection,
            ObservableCollection<ListBoxItemVM> femaleCollection
        )
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
        #endregion
        #region CreateFactionItem
        private ListBoxItemVM CreateFactionItem(string faction, string factionFile)
        {
            var item = new ListBoxItemVM();
            InitializeFactionItemData(item, faction, factionFile);
            return item;
        }

        private void InitializeFactionItemData(
            ListBoxItemVM item,
            string faction,
            string factionFile
        )
        {
            item.Id = faction;
            item.Name = GetVanillaFactionI18n(faction);
            item.Content = GetVanillaFactionI18n(faction);
            item.ToolTip = factionFile;
            item.Tag = this;
            item.ContextMenu = CreateFactionItemContextMenu(faction, factionFile);
        }

        private ContextMenuVM CreateFactionItemContextMenu(string faction, string factionFile)
        {
            return new(
                (list) =>
                {
                    list.Add(OpenDirectoryMenuItem());
                    list.Add(CleanPortraitsMenuItem());
                    list.Add(RenameFactionMenuItem());
                    list.Add(RemoveFactionMenuItem());
                }
            );
            MenuItemVM OpenDirectoryMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "打开文件";
                menuItem.CommandEvent += (o) =>
                {
                    if (o is not ListBoxItemVM item)
                        return;
                    Utils.OpenLink(Path.Combine(_BaseDirectory, item.ToolTip!.ToString()!));
                };
                return menuItem;
            }
            MenuItemVM CleanPortraitsMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "清除肖像";
                menuItem.ItemsSource = new();
                menuItem.Add(CleanAllPortraits());
                menuItem.Add(CleanMalePortraits());
                menuItem.Add(CleanFemalePortraits());
                return menuItem;
                MenuItemVM CleanAllPortraits()
                {
                    var menuItem = new MenuItemVM();
                    menuItem.Header = "清除所有肖像";
                    menuItem.CommandEvent += (o) =>
                    {
                        if (o is not ListBoxItemVM item)
                            return;
                        var faction = item.Id!.ToString()!;
                        CleanPortraits(faction, Gender.Male);
                        CleanPortraits(faction, Gender.Female);
                        RefreshDispalyData();
                    };
                    return menuItem;
                }
                MenuItemVM CleanMalePortraits()
                {
                    var menuItem = new MenuItemVM();
                    menuItem.Header = "清除男性肖像";
                    menuItem.CommandEvent += (o) =>
                    {
                        if (o is not ListBoxItemVM item)
                            return;
                        var faction = item.Id!.ToString()!;
                        CleanPortraits(faction, Gender.Male);
                        RefreshDispalyData();
                    };
                    return menuItem;
                }
                MenuItemVM CleanFemalePortraits()
                {
                    var menuItem = new MenuItemVM();
                    menuItem.Header = "清除女性肖像";
                    menuItem.CommandEvent += (o) =>
                    {
                        if (o is not ListBoxItemVM item)
                            return;
                        var faction = item.Id!.ToString()!;
                        CleanPortraits(faction, Gender.Female);
                        RefreshDispalyData();
                    };
                    return menuItem;
                }
                void CleanPortraits(string faction, Gender gender)
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
            MenuItemVM RenameFactionMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "重命名势力";
                menuItem.CommandEvent += (o) =>
                {
                    if (GroupId is PortraitsManagerViewModel._StrVanilla)
                    {
                        MessageBoxVM.Show(new("无法在原版重命名"));
                        return;
                    }
                    if (o is not ListBoxItemVM item)
                        return;
                    var faction = item.Id!.ToString()!;
                    if (_allFactionPortraits[faction].IsPortraitOnly)
                    {
                        MessageBoxVM.Show(new("此项包含除肖像以外的其它数据,无法重命名"));
                        return;
                    }
                    PrepareRenameFaction(item);
                };
                return menuItem;
            }
            MenuItemVM RemoveFactionMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "删除势力";
                menuItem.CommandEvent += (o) =>
                {
                    if (GroupId is PortraitsManagerViewModel._StrVanilla)
                    {
                        MessageBoxVM.Show(new("无法在原版删除势力"));
                        return;
                    }
                    if (o is not ListBoxItemVM item)
                        return;
                    var faction = item.Id!.ToString()!;
                    if (_allFactionPortraits[faction].IsPortraitOnly)
                    {
                        MessageBoxVM.Show(new("此项包含除肖像以外的其它数据,无法删除"));
                        return;
                    }
                    if (
                        MessageBoxVM.Show(
                            new("确定要删除势力吗")
                            {
                                Icon = MessageBoxVM.Icon.Question,
                                Button = MessageBoxVM.Button.YesNo
                            }
                        )
                        is not MessageBoxVM.Result.Yes
                    )
                        return;
                    RemoveFaction(faction);
                };
                return menuItem;
            }
        }

        private void PrepareRenameFaction(ListBoxItemVM item)
        {
            var faction = item.Id!.ToString()!;
            var viewModel = PortraitsManagerViewModel.Instance.AddFactionWindowViewModel;
            viewModel.OriginalFactionName = faction;
            viewModel.FactionName = faction;
            viewModel.BaseGroupData = this;
            viewModel.ShowDialog();
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
                    Utils.OpenLink(Path.Combine(_BaseDirectory, item.ToolTip!.ToString()!));
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
                        Path.Combine(_BaseDirectory, item.ToolTip!.ToString()!)
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
            Gender gender,
            bool forcedDeletion = false
        )
        {
            var autoDelete = forcedDeletion;
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
                                    + $"\n取消: 取消删除此文件  否: 只删除此文件  是: 删除并对接下来的唯一引用文件执行相同的操作"
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
            var count = selectedPortraitItems.Count;
            for (int i = 0; i < count; i++)
            {
                var item = selectedPortraitItems[i];
                var portraitPath = item.ToolTip!.ToString()!;
                UnreferenceAllPortrait(portraitPath);
                _planToDeletePortraitPaths.Add(portraitPath);
            }
            RefreshDispalyData();
        }

        private void DeleteAllPortraits()
        {
            foreach (var factionPortrait in _allFactionPortraits)
                factionPortrait.Value.Clear();
            foreach (var factionItems in MaleFactionPortraitItems)
                factionItems.Value.Clear();
            foreach (var factionItems in FemaleFactionPortraitItems)
                factionItems.Value.Clear();
            foreach (var portraitPath in _allImageStream)
                _planToDeletePortraitPaths.Add(portraitPath.Key);
        }

        private void UnreferenceAllPortrait(string portraitPath)
        {
            foreach (var factionPortrait in _allFactionPortraits)
            {
                factionPortrait.Value.Remove(portraitPath);
                var maleFactionPortraitItems = MaleFactionPortraitItems[
                    factionPortrait.Value.FactionId
                ];
                if (
                    maleFactionPortraitItems.FirstOrDefault(
                        i => i?.ToolTip?.ToString() == portraitPath,
                        null
                    )
                    is ListBoxItemVM maleItem
                )
                    maleFactionPortraitItems.Remove(maleItem);
                var femaleFactionPortraitItems = FemaleFactionPortraitItems[
                    factionPortrait.Value.FactionId
                ];
                if (
                    femaleFactionPortraitItems.FirstOrDefault(
                        i => i?.ToolTip?.ToString() == portraitPath,
                        null
                    )
                    is ListBoxItemVM femaleItem
                )
                    femaleFactionPortraitItems.Remove(femaleItem);
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

        private bool AddPortraitFile(string commonPath, string file, string faction, Gender gender)
        {
            if (!CheckFileIsImage(file))
                return false;
            var filePath = file.Replace(commonPath, "");
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var pmPortraitsPath = Path.Combine(_PMPortraitsDirectoryPath, filePath);
            var pmPortraitsFullName = Path.Combine(_PMPortraitsDirectory, filePath);
            var pmPortraitsDirectory = Path.GetDirectoryName(pmPortraitsFullName);
            if (!Directory.Exists(pmPortraitsDirectory))
                Directory.CreateDirectory(pmPortraitsDirectory!);
            Stream stream;
            if (_allImageStream.ContainsKey(pmPortraitsPath))
            {
                stream = _allImageStream[pmPortraitsPath];
            }
            else
            {
                File.Copy(file, pmPortraitsFullName, true);
                stream = new StreamReader(pmPortraitsFullName).BaseStream;
                _allImageStream.Add(pmPortraitsPath, stream);
            }
            var factionPortrait = _allFactionPortraits[faction];
            factionPortrait.Add(pmPortraitsPath, gender);
            var portraitItem = CreatePortraitItem(
                gender,
                faction,
                fileName,
                pmPortraitsPath,
                stream
            );
            if (gender is Gender.Male)
                MaleFactionPortraitItems[faction].Add(portraitItem);
            else
                FemaleFactionPortraitItems[faction].Add(portraitItem);
            return true;
        }

        private static bool CheckFileIsImage(string file)
        {
            using FileStream fs = new(file, FileMode.Open);
            var head = new byte[4];
            fs.Read(head);
            Array.Reverse(head);
            var pngHead = BitConverter.ToUInt32(head);
            if (pngHead is 0x89504E47)
            {
                fs.Seek(16, SeekOrigin.Begin);
                fs.Read(head);
                Array.Reverse(head);
                int width = BitConverter.ToInt32(head);
                fs.Read(head);
                Array.Reverse(head);
                int height = BitConverter.ToInt32(head);
                fs.Close();
                if (width is 128 && height is 128)
                    return true;
            }
            return false;
        }
        #endregion
        #region Save
        public async void Save()
        {
            if (IsChangeed is false)
                return;
            using var handler = PendingBoxVM.Show("正在保存");
            await Task.Delay(1);
            DeletePlanToDeleteFactions();
            await SaveAllFactionPortrait();
            DeletePlanToDeleteFiles();
            IsChangeed = false;
        }

        private async Task SaveAllFactionPortrait()
        {
            bool isChanged = false;
            foreach (var factionPortrait in _allFactionPortraits)
            {
                if (!factionPortrait.Value.IsChanged)
                    continue;
                if (!isChanged)
                {
                    await BackupFromOriginalFile();
                    isChanged = true;
                }
                factionPortrait.Value.Save();
            }
        }

        private async Task BackupToArchiveFile(string destDirectory)
        {
            // 备份数据至压缩文件
            CreateTempBackupDirectory();
            // 保存势力数据
            foreach (var factionPortrait in _allFactionPortraits)
                factionPortrait.Value.SaveTo(_PMTempFactionsBackupDirectory);
            // 保存肖像数据
            BackupPortraits(_allImageStream.Keys);
            await ArchiveTempBackupDirectoryToFile(destDirectory);
        }

        private async Task BackupFromOriginalFile()
        {
            // 备份原始文件然后做成压缩包
            CreateBackupDirectory();
            CreateTempBackupDirectory();
            if (File.Exists(_PMBackupZIPFile))
                return;
            if (Utils.GetAllSubFiles(_FactionsDirectory) is not List<FileInfo> fileList)
                return;
            // 备份势力并获取所有引用的肖像
            var portraitPaths = BackupFactionsFromOriginalFile(fileList);
            // 备份所有引用的肖像
            BackupPortraits(portraitPaths);
            await ArchiveTempBackupDirectoryToFile(_PMBackupDirectory);
        }

        private HashSet<string> BackupFactionsFromOriginalFile(IList<FileInfo> fileList)
        {
            var portraitPaths = new HashSet<string>();
            foreach (var file in fileList)
            {
                if (
                    FactionPortrait.Create(file.FullName, _BaseDirectory, out _)
                    is not FactionPortrait factionPortrait
                )
                    continue;
                foreach (var portraitPath in factionPortrait.AllPortraitsPath)
                    portraitPaths.Add(portraitPath);
                factionPortrait.SaveTo(_PMTempFactionsBackupDirectory);
            }
            return portraitPaths;
        }

        private void BackupPortraits(IEnumerable<string> portraitPaths)
        {
            // 基于 _BaseDirectory 构建文件树
            // 存入 _PMTempPortraitsBackupDirectory
            foreach (var path in portraitPaths)
            {
                var sourceFile = Path.Combine(_BaseDirectory, path);
                var destFile = Path.Combine(_PMTempPortraitsBackupDirectory, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                File.Copy(sourceFile, destFile);
            }
        }

        private void DeletePlanToDeleteFiles()
        {
            // 通过排序来优先删除子文件夹的内容
            foreach (var portraitPath in _planToDeletePortraitPaths.OrderBy(s => s))
            {
                _allImageStream[portraitPath].Close();
                _allImageStream.Remove(portraitPath);
                var portraitFile = Path.Combine(_BaseDirectory, portraitPath);
                if (!TryDeletePortraitDirectory(portraitFile))
                    Utils.DeleteFileToRecycleBin(portraitFile);
            }
            _planToDeletePortraitPaths.Clear();
        }

        private void DeletePlanToDeleteFactions()
        {
            foreach (var faction in _planToDeleteFaction)
            {
                var file = FactionPortrait.CombineFactionPath(_FactionsDirectory, faction);
                Utils.DeleteFileToRecycleBin(file);
            }
            _planToDeleteFaction.Clear();
        }

        private bool TryDeletePortraitDirectory(string portraitFile)
        {
            var portraitDirectory = Path.GetDirectoryName(portraitFile)!;
            if (portraitDirectory.EndsWith(_PMPortraitsDirectoryPath))
                return false;
            if (Directory.GetFiles(portraitDirectory)?.Length is 1)
            {
                Utils.DeleteDirToRecycleBin(portraitDirectory);
                return true;
            }
            return false;
        }

        private async Task ArchiveTempBackupDirectoryToFile(string destDirectory)
        {
            // 生成压缩文件名
            var archiveFileName =
                $"{GroupId} {nameof(FactionPortrait)} {DateTime.Now:yyyy-MM-ddTHH-mm-ss}";
            await Utils.ArchiveDirectoryToFile(
                _PMTempBackupDirectory,
                destDirectory,
                archiveFileName
            );
            // 完成后删除临时备份文件夹
            DeleteTempBackupDirectory();
        }

        private void CreateBackupDirectory()
        {
            Directory.CreateDirectory(_PMDirectory);
            Directory.CreateDirectory(_PMBackupDirectory);
        }

        private void CreateTempBackupDirectory()
        {
            Directory.CreateDirectory(_PMTempFactionsBackupDirectory);
            Directory.CreateDirectory(_PMTempPortraitsBackupDirectory);
        }

        private void DeleteTempBackupDirectory()
        {
            Directory.Delete(_PMTempBackupDirectory, true);
        }

        #endregion
        #region Close
        public void Close()
        {
            foreach (var kv in _allImageStream)
                kv.Value.Close();
            _allImageStream.Clear();
            _allFactionPortraits.Clear();
            _planToDeleteFaction.Clear();
            _planToDeletePortraitPaths.Clear();
            FactionList.Clear();
            ContextMenu.Clear();
            MaleFactionPortraitItems.Clear();
            FemaleFactionPortraitItems.Clear();
        }
        #endregion
        private static string GetVanillaFactionI18n(string factionId) =>
            VanillaFactions.AllVanillaFactionsI18n.TryGetValue(factionId, out var name)
                ? name
                : factionId;
    }
}
