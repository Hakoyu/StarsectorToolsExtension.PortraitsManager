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

        public bool IsChanged { get; private set; }

        private readonly string _StarsectorToolsExtensionPortraitsManager =
            $"{nameof(StarsectorToolsExtension)}.PortraitsManager";

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
        private readonly string _PMOriginalBackupFile;

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
            _header =
                $"{vaule} ({_allFactionPortraits.Count},{_allImageStream.Count - _planToDeletePortraitPaths.Count})";
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

        private readonly HashSet<string> _planToDeleteFactions = new();
        private readonly HashSet<string> _planToDeletePortraitPaths = new();

        private GroupData(string groupId, string groupName, string baseDirectory)
        {
            ToolTip = groupName;
            GroupId = groupId;

            // 初始化目录数据
            _BaseDirectory = baseDirectory;
            _FactionsDirectory = $"{_BaseDirectory}\\data\\world\\factions";
            _PortraitsDirectory = $"{_BaseDirectory}\\graphics\\portraits";
            _PMDirectory = $"{_BaseDirectory}\\{_StarsectorToolsExtensionPortraitsManager}";
            _PMPortraitsDirectory = $"{_PMDirectory}\\Portraits";
            _PMPortraitsDirectoryPath = $"{_StarsectorToolsExtensionPortraitsManager}\\Portraits";
            _PMBackupDirectory = $"{_PMDirectory}\\Backup";
            _PMOriginalBackupFile =
                $"{_PMBackupDirectory}\\{GroupId} Original{nameof(FactionPortrait)}.zip";
            _PMFactionsBackupDirectory = $"{_PMBackupDirectory}\\Faction";
            _PMPortraitsBackupDirectory = $"{_PMBackupDirectory}\\Portraits";
            _PMTempBackupDirectory = $"{_PMBackupDirectory}\\Backup";
            _PMTempFactionsBackupDirectory = $"{_PMBackupDirectory}\\Backup\\Factions";
            _PMTempPortraitsBackupDirectory = $"{_PMBackupDirectory}\\Backup\\Portraits";

            // 初始化数据
            InitializeData();
        }

        #region InitializeData
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
                menuItem.Header = "导出至压缩文件";
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
                            Save();
                    }
                    CreateBackupDirectory();
                    var file = SaveFileDialogVM.Show(
                        new()
                        {
                            Title = "选择保存的压缩文件",
                            Filter = $"Zip 文件|*.zip",
                            InitialDirectory = _PMBackupDirectory
                        }
                    );
                    if (string.IsNullOrWhiteSpace(file))
                        return;
                    await BackupToArchiveFile(
                        Path.GetDirectoryName(file)!,
                        Path.GetFileNameWithoutExtension(file)
                    );
                };
                return menuItem;
            }
            MenuItemVM RestoreBackupMenuItem()
            {
                var menuItem = new MenuItemVM();
                menuItem.Header = "从压缩文件还原";
                menuItem.CommandEvent += async (o) =>
                {
                    if (PrepareRestoreBackup() is not string archiveFile)
                    {
                        DeleteTempBackupDirectory();
                        return;
                    }
                    try
                    {
                        if (await UnArchiveAndReplaceFiles(archiveFile) is false)
                        {
                            DeleteTempBackupDirectory();
                            return;
                        }
                        TryDeleteAllImages();
                        MovePortraits();
                        // 初始化数据
                        InitializeData();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"文件错误 {archiveFile}", ex);
                        MessageBoxVM.Show(new($"文件错误\n{archiveFile}") { Icon = MessageBoxVM.Icon.Error });
                    }
                    finally
                    {
                        DeleteTempBackupDirectory();
                        PortraitsManagerViewModel.Instance.CleanShowPortraitItems();
                        RefreshDispalyData(false);
                    }
                };
                return menuItem;

                string? PrepareRestoreBackup()
                {
                    // 如果未保存,提醒保存
                    if (IsChanged)
                    {
                        if (
                            MessageBoxVM.Show(
                                new("从备份中还原会丢失当前数据,推荐先备份当前数据再继续,确定要继续吗?")
                                {
                                    Icon = MessageBoxVM.Icon.Question,
                                    Button = MessageBoxVM.Button.YesNo
                                }
                            ) is MessageBoxVM.Result.No
                        )
                            return null;
                    }
                    CreateBackupDirectory();
                    // 获取文件
                    var fileNames = OpenFileDialogVM.Show(
                        new()
                        {
                            Title = "选择压缩文件",
                            Filter = $"Zip 文件|*.zip",
                            InitialDirectory = _PMBackupDirectory
                        }
                    );
                    return fileNames?.FirstOrDefault(defaultValue: null);
                }

                async Task<bool> UnArchiveAndReplaceFiles(string archiveFile)
                {
                    CreateTempBackupDirectory();
                    if (
                        await Utils.UnArchiveFileToDirectory(archiveFile, _PMTempBackupDirectory)
                        is false
                    )
                    {
                        MessageBoxVM.Show(new("文件解压错误"));
                        return false;
                    }
                    if (
                        Utils.GetAllSubFiles(_PMTempFactionsBackupDirectory)
                        is not List<FileInfo> fileList || !fileList.Any()
                    )
                        return false;
                    // 尝试备份原始数据
                    await BackupOriginalData();
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
                    return true;
                }
                void TryDeleteAllImages()
                {
                    // 如果不是原版,则清除肖像
                    if (GroupId != PortraitsManagerViewModel._StrVanilla)
                    {
                        // 获取引用的肖像路径
                        var imagePaths = _allImageStream.Keys.ToList();
                        // 清空所有数据
                        Close();
                        // 删除引用的肖像
                        foreach (var imagePath in imagePaths)
                            File.Delete(Path.Combine(_BaseDirectory, imagePath));
                    }
                    else
                        Close();
                }
                void MovePortraits()
                {
                    // 获取上层文件夹名
                    var directoryName = Path.GetFileName(_BaseDirectory);
                    var portraitDirectory = Path.Combine(_PMTempBackupDirectory, directoryName);
                    // 移动至新文件夹(重命名至上层文件夹名,让接下来的移动操作变成替换)
                    Directory.Move(_PMTempPortraitsBackupDirectory, portraitDirectory);
                    // 移动肖像以替换原始文件
                    Utils.MoveDirectory(portraitDirectory, Path.GetDirectoryName(_BaseDirectory)!);
                }
            }
        }
        #endregion
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
            _planToDeleteFactions.Remove(faction);
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

            _planToDeleteFactions.Remove(faction);
            _planToDeleteFactions.Remove(newFaction);
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
            _planToDeleteFactions.Add(faction);
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
            IsChanged = isRemindSave;
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

        #endregion ParseBaseDirectory

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

        #endregion CreateFactionItem

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
                    if (GroupId == PortraitsManagerViewModel._StrVanilla)
                        return;
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

        #endregion TryRemoveSelectedPortraits

        #region TryDeleteSelectedPortraits

        private void DeleteSelectedPortraits(string faction, Gender gender)
        {
            if (GroupId == PortraitsManagerViewModel._StrVanilla)
                return;
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
            if (GroupId == PortraitsManagerViewModel._StrVanilla)
                return;
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

        #endregion TryDeleteSelectedPortraits

        #endregion CreatePortraitContextMenu

        #region TryAddPortrait

        public void TryAddPortrait(IEnumerable<string> pathList, string faction, Gender gender)
        {
            StringBuilder errSB = new();
            string commonPath =
                Path.GetFullPath(Path.Combine(pathList.Select(Path.GetDirectoryName).ToArray()!))
                + "\\";
            if (commonPath.StartsWith(_PMPortraitsDirectory))
                commonPath = _BaseDirectory + "\\";
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
            var filePath = file.Replace(commonPath, "");
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var pmPortraitsPath = Path.Combine(_PMPortraitsDirectoryPath, filePath);
            Stream stream;
            // 判断是否已载入资源
            if (_allImageStream.ContainsKey(filePath))
            {
                stream = _allImageStream[filePath];
                pmPortraitsPath = filePath;
            }
            else if (_allImageStream.ContainsKey(pmPortraitsPath))
            {
                stream = _allImageStream[pmPortraitsPath];
            }
            else
            {
                var pmPortraitsFullName = Path.Combine(_PMPortraitsDirectory, filePath);
                var pmPortraitsDirectory = Path.GetDirectoryName(pmPortraitsFullName);
                if (!CheckFileIsImage(file))
                    return false;
                if (!Directory.Exists(pmPortraitsDirectory))
                    Directory.CreateDirectory(pmPortraitsDirectory!);
                _planToDeletePortraitPaths.Remove(pmPortraitsPath);
                File.Copy(file, pmPortraitsFullName, true);
                stream = new StreamReader(pmPortraitsFullName).BaseStream;
                _allImageStream.Add(pmPortraitsPath, stream);
            }
            var factionPortrait = _allFactionPortraits[faction];
            factionPortrait.Add(pmPortraitsPath, gender);
            ObservableCollection<ListBoxItemVM> factionPortraitItems;
            if (gender is Gender.Male)
                factionPortraitItems = MaleFactionPortraitItems[faction];
            else
                factionPortraitItems = FemaleFactionPortraitItems[faction];
            // 判断是否存在相同项
            if (!factionPortraitItems.Any(i => i.ToolTip!.ToString() == pmPortraitsPath))
            {
                var portraitItem = CreatePortraitItem(
                    gender,
                    faction,
                    fileName,
                    pmPortraitsPath,
                    stream
                );
                factionPortraitItems.Add(portraitItem);
            }
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

        #endregion TryAddPortrait

        #region Save

        public async void Save()
        {
            if (IsChanged is false)
                return;
            using var handler = PendingBoxVM.Show("正在保存");
            DeletePlanToDeleteFactions();
            var success = await SaveAllFactionPortrait();
            DeletePlanToDeletePortraitFiles();
            IsChanged = success;
        }

        private async Task<bool> SaveAllFactionPortrait()
        {
            bool anyChanged = false;
            foreach (var factionPortrait in _allFactionPortraits)
            {
                if (!factionPortrait.Value.IsChanged)
                    continue;
                if (!anyChanged)
                {
                    await BackupOriginalData();
                    anyChanged = true;
                }
                if (GroupId == PortraitsManagerViewModel._StrVanilla)
                {
                    if (
                        factionPortrait.Value.MalePortraitsPath.Count is 0
                        || factionPortrait.Value.FemalePortraitsPath.Count is 0
                    )
                    {
                        MessageBoxVM.Show(new("保存失败,在原版中,男女至少各一个肖像"));
                        return false;
                    }
                }
                factionPortrait.Value.Save();
            }
            return true;
        }

        private async Task BackupToArchiveFile(string destDirectory, string archiveFileName)
        {
            // 备份数据至压缩文件
            CreateTempBackupDirectory();
            // 保存势力数据
            foreach (var factionPortrait in _allFactionPortraits)
                factionPortrait.Value.SaveTo(_PMTempFactionsBackupDirectory);
            // 保存肖像数据
            BackupPortraits(_allImageStream.Keys);
            await ArchiveTempBackupDirectoryToFile(destDirectory, archiveFileName);
        }

        private async Task BackupOriginalData()
        {
            // 备份原始文件然后做成压缩包
            CreateBackupDirectory();
            if (File.Exists(_PMOriginalBackupFile))
                return;
            CreateTempBackupDirectory();
            if (Utils.GetAllSubFiles(_FactionsDirectory) is not List<FileInfo> fileList)
                return;
            // 保存势力数据
            var portraitPaths = BackupOriginalFactions(fileList);
            // 保存肖像数据
            BackupPortraits(portraitPaths);
            await ArchiveTempBackupDirectoryToFile(_PMBackupDirectory, _PMOriginalBackupFile);
        }

        private HashSet<string> BackupOriginalFactions(IList<FileInfo> fileList)
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

        private void DeletePlanToDeletePortraitFiles()
        {
            CreateTempBackupDirectory();
            var portraitPaths = _planToDeletePortraitPaths.OrderBy(s => s);
            // 通过备份肖像的方式,来删除整个文件夹而不是单独删除每个问题
            BackupPortraits(portraitPaths);
            // 通过排序来优先删除子文件夹的内容
            foreach (var portraitPath in portraitPaths)
            {
                _allImageStream[portraitPath].Close();
                _allImageStream.Remove(portraitPath);
                var portraitFile = Path.Combine(_BaseDirectory, portraitPath);
                // 如果无法删除文件夹则删除文件
                if (!TryDeletePortraitDirectory(portraitFile))
                    File.Delete(portraitFile);
            }
            // 最后将构建的文件树删除至回收站
            Utils.DeleteDirectoryToRecycleBin(_PMTempPortraitsBackupDirectory);
            DeleteTempBackupDirectory();
            _planToDeletePortraitPaths.Clear();
        }

        private void DeletePlanToDeleteFactions()
        {
            if (!_planToDeleteFactions.Any())
                return;
            CreateTempBackupDirectory();
            foreach (var faction in _planToDeleteFactions)
            {
                var file = FactionPortrait.CombineFactionPath(_FactionsDirectory, faction);
                var destFile = FactionPortrait.CombineFactionPath(
                    _PMTempFactionsBackupDirectory,
                    faction
                );
                // 将肖像移动至文件夹来统一删除
                File.Move(file, destFile);
            }
            // 删除肖像文件夹
            Utils.DeleteFileToRecycleBin(_PMTempFactionsBackupDirectory);
            DeleteTempBackupDirectory();
            _planToDeleteFactions.Clear();
        }

        private bool TryDeletePortraitDirectory(string portraitFile)
        {
            var portraitDirectory = Path.GetDirectoryName(portraitFile)!;
            // 如果上层文件夹是_PMPortraitsDirectoryPath,则不删除
            if (portraitDirectory.EndsWith(_PMPortraitsDirectoryPath))
                return false;
            // 如果删除文件是文件夹中的唯一,则删除文件夹
            if (Directory.GetFiles(portraitDirectory)?.Length is not 1)
                return false;
            Directory.Delete(portraitDirectory);
            return true;
        }

        private async Task ArchiveTempBackupDirectoryToFile(
            string destDirectory,
            string archiveFileName
        )
        {
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
            if (Directory.Exists(_PMTempBackupDirectory))
                Directory.Delete(_PMTempBackupDirectory, true);
        }

        #endregion Save

        #region Close

        public void Close()
        {
            foreach (var kv in _allImageStream)
                kv.Value.Close();
            _allImageStream.Clear();
            _allFactionPortraits.Clear();
            _planToDeleteFactions.Clear();
            _planToDeletePortraitPaths.Clear();
            FactionList.Clear();
            ContextMenu.Clear();
            MaleFactionPortraitItems.Clear();
            FemaleFactionPortraitItems.Clear();
        }

        #endregion Close

        private static string GetVanillaFactionI18n(string factionId) =>
            VanillaFactions.AllVanillaFactionsI18n.TryGetValue(factionId, out var name)
                ? name
                : factionId;
    }
}
