using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HKW.Libs.Log4Cs;
using HKW.ViewModels.Dialogs;
using StarsectorTools.Libs.GameInfo;
using StarsectorTools.Libs.Utils;

namespace StarsectorToolsExtension.PortraitsManager.Views
{
    public partial class PortraitsManagerPage
    {
        public const string strVanilla = "Vanilla";
        private const string strFactionExtension = ".faction";
        private const string strStandardMale = "standard_male";
        private const string strStandardFemale = "standard_female";
        private const string strPortraits = "portraits";
        private const string strId = "id";
        private string nowGroup = string.Empty;
        private string nowFaction = string.Empty;

        /// <summary>
        /// <para>全部分组信息</para>
        /// <para><see langword="Key"/>: Group</para>
        /// <para><see langword="Value"/>: GroupData</para>
        /// </summary>
        private Dictionary<string, GroupData> allGroupDatas = new();

        /// <summary>
        /// 分组信息
        /// </summary>
        class GroupData
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

            /// <summary>备份目录</summary>
            public string PMBackupDirectory { get; private set; }

            /// <summary>势力备份目录</summary>
            public string PMFactionBackupDirectory { get; private set; }

            /// <summary>下拉菜单</summary>
            public Expander Expander { get; private set; }

            /// <summary>
            /// <para>所有显示于下拉菜单中的势力列表项</para>
            /// <para><see langword="Key"/>: FactionId</para>
            /// <para><see langword="Value"/>: ListBoxItem</para>
            /// </summary>
            public ConcurrentDictionary<string, ListBoxItem> AllFactionItems { get; private set; } =
                new();

            /// <summary>
            /// <para>全部势力的肖像</para>
            /// <para><see langword="Key"/>: FactionId</para>
            /// <para><see langword="Value"/>: FactionPortraits</para>
            /// </summary>
            public ConcurrentDictionary<string, FactionPortraits> AllFactionPortraits
            {
                get;
                private set;
            } = new();

            /// <summary>
            /// <para>全部肖像的使用状态</para>
            /// <para><see langword="Key"/>: PortraitPath</para>
            /// <para><see langword="Value"/>: FactionId => Gender</para>
            /// </summary>
            public ConcurrentDictionary<
                string,
                ConcurrentDictionary<string, Gender>
            > AllPortraitsUseStatus { get; private set; } = new();

            /// <summary>
            /// <para>全部肖像的图像资源</para>
            /// <para><see langword="Key"/>: PortraitPath</para>
            /// <para><see langword="Value"/>: BitmapImage</para>
            /// </summary>
            public ConcurrentDictionary<string, BitmapImage> AllPortraitImages
            {
                get;
                private set;
            } = new();

            /// <summary>
            /// <para>显示于男性图表中的列表项</para>
            /// <para><see langword="Key"/>: PortraitPath</para>
            /// <para><see langword="Value"/>: ListBoxItem</para>
            /// </summary>
            public ConcurrentDictionary<string, ListBoxItem> AllMalePortraitItems
            {
                get;
                private set;
            } = new();

            /// <summary>
            /// <para>显示于女性图表中的列表项</para>
            /// <para><see langword="Key"/>: PortraitPath</para>
            /// <para><see langword="Value"/>: ListBoxItem</para>
            /// </summary>
            public ConcurrentDictionary<string, ListBoxItem> AllFemalePortraitItems
            {
                get;
                private set;
            } = new();

            /// <summary>
            /// <para>全部计划删除的文件</para>
            /// <para><see langword="Key"/>: PortraitPath</para>
            /// <para><see langword="Value"/>: PortraitFullPath</para>
            /// </summary>
            public ConcurrentDictionary<string, string> AllPlanToDeleteFiles { get; private set; } =
                new();

            public GroupData(string group, string baseDirectory, Expander expander)
            {
                GroupId = group;
                Expander = expander;
                BaseDirectory = baseDirectory;
                FactionDirectory = $"{baseDirectory}\\data\\world\\factions";
                PMDirectory =
                    $"{baseDirectory}\\{nameof(StarsectorToolsExtension)}.{nameof(PortraitsManagerPage)}";
                PMPortraitsDirectory = $"{PMDirectory}\\Portraits";
                PMBackupDirectory = $"{PMDirectory}\\Backup";
                PMFactionBackupDirectory = $"{PMBackupDirectory}\\Faction";
            }

            public void CloseAllImageSources()
            {
                foreach (var portraitImage in AllPortraitImages)
                    portraitImage.Value?.StreamSource?.Close();
            }
        }

        public void Close()
        {
            CloseAllGroupDatasImageSources();
        }

        private void CloseAllGroupDatasImageSources()
        {
            // 清除所有占用的图像资源
            foreach (var groupData in allGroupDatas)
                groupData.Value.CloseAllImageSources();
        }

        public void Save()
        {
            SaveAllData();
        }

        private string GetVanillaFactionI18n(string factionId) =>
            VanillaFactions.AllVanillaFactionsI18n.TryGetValue(factionId, out var name)
                ? name
                : factionId;

        private async void ChangeGroupsType(string groupType)
        {
            StackPanel_GroupList.Children.Clear();
            CloseAllGroupDatasImageSources();
            allGroupDatas.Clear();
            nowGroup = string.Empty;
            nowFaction = string.Empty;
            RefreshPortraits(groupType, nowFaction);
            if (groupType is strVanilla)
            {
                await AddExpander(groupType, "原版", GameInfo.CoreDirectory);
                allGroupDatas[groupType].Expander.IsExpanded = true;
            }
            else if (groupType is ModTypeGroup.Enabled)
            {
                //foreach (var modId in ModsInfo.AllEnabledModsId)
                //{
                //    var modInfo = ModsInfo.AllModsInfo[modId];
                //    await AddExpander(modId, modInfo.Name, modInfo.ModDirectory);
                //}
            }
            else if (groupType is ModTypeGroup.Collected) { }
            else { }
            GC.Collect();
        }

        private async Task AddExpander(string group, string groupName, string baseDirectory)
        {
            Expander expander = null!;
            expander = new Expander
            {
                Header = groupName,
                ToolTip = groupName,
                Padding = new() { Left = 3, Right = 3 },
                Style = (Style)Application.Current.Resources["ExpanderBaseStyle"],
            };
            allGroupDatas.Add(group, new(group, baseDirectory, expander));
            var listBox = await GetFactionListBox(group, baseDirectory);
            var contextMenu = CreateGroupExpanderContextMenu(group);
            expander.Content = listBox;
            expander.ContextMenu = contextMenu;
            RefreshGroupImagesCount(group);
            CreatePMDirectory(group);
            StackPanel_GroupList.Children.Add(expander);
        }

        private void CreatePMDirectory(string group)
        {
            var groupData = allGroupDatas[group];
            if (!Directory.Exists(groupData.PMDirectory))
                Directory.CreateDirectory(groupData.PMDirectory);
        }

        private ContextMenu CreateGroupExpanderContextMenu(string group)
        {
            return Dispatcher.Invoke(() =>
            {
                var contextMenu = new ContextMenu();
                // 标记菜单项是否被创建
                contextMenu.Tag = false;
                // 被点击时才加载菜单,可以降低内存占用
                contextMenu.Loaded += (s, e) =>
                {
                    if (contextMenu.Tag is true)
                        return;
                    contextMenu.Style = (Style)Application.Current.Resources["ContextMenuBaseStyle"];
                    // 添加势力
                    var menuItem = new MenuItem();
                    menuItem.Header = "添加势力";
                    menuItem.Style = (Style)Application.Current.Resources["MenuItemBaseStyle"];
                    menuItem.Click += (s, e) => { };
                    contextMenu.Items.Add(menuItem);

                    contextMenu.Tag = true;
                };
                return contextMenu;
            });
        }

        private void AddFaction(string group, string factionId)
        {
            if (group is strVanilla)
            {
                MessageBoxVM.Show(new("原版无法清除势力") { Icon = MessageBoxVM.Icon.Warning });
                return;
            }
        }

        private async Task<ListBox?> GetFactionListBox(string group, string baseDirectory)
        {
            var listBox = new ListBox
            {
                Style = (Style)Application.Current.Resources["ListBoxBaseStyle"],
                Tag = group,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            // 子项选择事件
            listBox.SelectionChanged += (s, e) =>
            {
                if (s is ListBox listBox && listBox.SelectedItem is ListBoxItem item)
                {
                    nowGroup = listBox.Tag.ToString()!;
                    nowFaction = item.Tag.ToString()!;
                    RefreshPortraits(nowGroup, nowFaction);
                }
            };
            // 使滚轮事件传到父级
            listBox.PreviewMouseWheel += (s, e) =>
            {
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = MouseWheelEvent,
                    Source = s,
                };
                if (s is Control control && control.Parent is UIElement ui)
                    ui.RaiseEvent(eventArg);
                e.Handled = true;
            };
            // 遍历基目录下的势力文件夹
            if (
                Utils.GetAllSubFiles(allGroupDatas[group].FactionDirectory)
                is not List<FileInfo> fileList
            )
                return null;
            //await Parallel.ForEachAsync(
            //    fileList,
            //    async (file, _) =>
            //    {
            //        FactionPortraits portraits = new(file.FullName, baseDirectory);
            //        if (portraits.HasPortrait)
            //        {
            //            var item = CreateFactionItems(group, baseDirectory, portraits);
            //            await Dispatcher.InvokeAsync(() => listBox.Items.Add(item));
            //        }
            //        if (portraits.ErrorMessage is not null)
            //        {
            //            Logger.Warring(portraits.ErrorMessage);
            //            MessageBoxVM.Show(
            //                new(portraits.ErrorMessage) { Icon = MessageBoxVM.Icon.Warning }
            //            );
            //        }
            //    }
            //);
            //foreach (var file in fileList)
            //{
            //    FactionPortraits portraits = new(file.FullName, baseDirectory);
            //    if (portraits.HasPortrait)
            //    {
            //        var item = CreateFactionItems(group, baseDirectory, portraits);
            //        await Dispatcher.InvokeAsync(() => listBox.Items.Add(item));
            //    }
            //    if (portraits.ErrorMessage is not null)
            //    {
            //        STLog.WriteLine(portraits.ErrorMessage, STLogLevel.WARN);
            //        Utils.ShowMessageBox(portraits.ErrorMessage, STMessageBoxIcon.Warning);
            //    }
            //}
            if (listBox.Items.Count == 0)
                return null;
            return listBox;
        }

        private ContextMenu CreateFactionItemContextMenu(string group, string factionId)
        {
            var contextMenu = new ContextMenu();
            // 标记菜单项是否被创建
            contextMenu.Tag = false;
            // 被点击时才加载菜单,可以降低内存占用
            contextMenu.Loaded += (s, e) =>
            {
                if (contextMenu.Tag is true)
                    return;
                contextMenu.Style = (Style)Application.Current.Resources["ContextMenuBaseStyle"];
                // 清空势力引用的所有肖像
                var menuItem = new MenuItem();
                menuItem.Header = "清空引用肖像";
                menuItem.Style = (Style)Application.Current.Resources["MenuItemBaseStyle"];
                menuItem.Click += (s, e) =>
                {
                    var groupData = allGroupDatas[group];
                    foreach (var path in groupData.AllFactionPortraits[factionId].AllPortraitsPath)
                    {
                        if (groupData.AllMalePortraitItems.ContainsKey(path))
                            RemoveMalePortrait(group, factionId, path);
                        if (groupData.AllFemalePortraitItems.ContainsKey(path))
                            RemoveFemalePortrait(group, factionId, path);
                    }
                    RefreshPortraits(group, factionId);
                    RefreshGroupImagesCount(group);
                };
                contextMenu.Items.Add(menuItem);
                // 删除此势力
                menuItem = new MenuItem();
                menuItem.Header = "删除此势力";
                menuItem.Style = (Style)Application.Current.Resources["MenuItemBaseStyle"];
                menuItem.Click += (s, e) =>
                {
                    RemoveFaction(group, factionId);
                };
                contextMenu.Items.Add(menuItem);

                contextMenu.Tag = true;
            };
            return contextMenu;
        }

        private void RemoveFaction(string group, string factionId)
        {
            if (group is strVanilla)
            {
                MessageBoxVM.Show(new("原版无法清除势力") { Icon = MessageBoxVM.Icon.Warning });
                return;
            }
            if (
                MessageBoxVM.Show(
                    new($"确认删除势力: {factionId}?\n此操作将会删除势力文件")
                    {
                        Button = MessageBoxVM.Button.YesNo,
                        Icon = MessageBoxVM.Icon.Question
                    }
                ) is MessageBoxVM.Result.No
            )
                return;
            var groupData = allGroupDatas[group];
            foreach (var path in groupData.AllFactionPortraits[factionId].AllPortraitsPath)
            {
                if (groupData.AllMalePortraitItems.ContainsKey(path))
                    RemoveMalePortrait(group, factionId, path);
                if (groupData.AllFemalePortraitItems.ContainsKey(path))
                    RemoveFemalePortrait(group, factionId, path);
            }
            groupData.AllPlanToDeleteFiles.TryAdd(
                groupData.AllFactionPortraits[factionId].FileFullName,
                groupData.AllFactionPortraits[factionId].FileFullName
            );
            groupData.AllFactionPortraits.Remove(factionId, out var _);
            groupData.AllFactionItems.Remove(factionId, out var _);
            RefreshPortraits(group);
            RefreshGroup(group);
        }

        private ListBoxItem CreateFactionItems(
            string group,
            string baseDirectory,
            FactionPortraits portraits
        )
        {
            var groupData = allGroupDatas[group];
            string factionName = GetVanillaFactionI18n(portraits.FactionId);
            var listBoxItem = Dispatcher.Invoke(() =>
            {
                return new ListBoxItem()
                {
                    ToolTip = factionName,
                    Content =
                        $"{factionName} ({portraits.MalePortraitsPath.Count},{portraits.FemalePortraitsPath.Count})",
                    Style = (Style)Application.Current.Resources["ListBoxItemBaseStyle"],
                    ContextMenu = CreateFactionItemContextMenu(group, portraits.FactionId),
                    Tag = portraits.FactionId,
                    Padding = new() { Left = 5, Right = 5, }
                };
            });
            groupData.AllFactionItems.TryAdd(portraits.FactionId, listBoxItem);
            groupData.AllFactionPortraits.TryAdd(portraits.FactionId, portraits);
            foreach (var portraitsPath in portraits.AllPortraitsPath)
            {
                if (portraits.MalePortraitsPath.Contains(portraitsPath))
                    AddPortraitItem(
                        group,
                        portraits.FactionId,
                        baseDirectory,
                        portraitsPath,
                        Gender.Male
                    );
                if (portraits.FemalePortraitsPath.Contains(portraitsPath))
                    AddPortraitItem(
                        group,
                        portraits.FactionId,
                        baseDirectory,
                        portraitsPath,
                        Gender.Female
                    );
            }
            return listBoxItem;
        }

        private void AddPortraitItem(
            string group,
            string factionId,
            string baseDirectory,
            string portraitPath,
            Gender gender
        )
        {
            var groupData = allGroupDatas[group];
            // 获取图像数据
            if (!groupData.AllPortraitImages.TryGetValue(portraitPath, out var bitmapImage))
            {
                bitmapImage = GetBitmapImage(Path.Combine(baseDirectory, portraitPath));
                groupData.AllPortraitImages.TryAdd(portraitPath, bitmapImage);
            }
            // 添加到势力肖像
            groupData.AllFactionPortraits[factionId].Add(portraitPath, gender);
            // 添加到显示项中
            if (gender is Gender.Male)
                groupData.AllMalePortraitItems.TryAdd(
                    portraitPath,
                    CreateShowPortraitItem(group, factionId, portraitPath, bitmapImage, gender)
                );
            else if (gender is Gender.Female)
                groupData.AllFemalePortraitItems.TryAdd(
                    portraitPath,
                    CreateShowPortraitItem(group, factionId, portraitPath, bitmapImage, gender)
                );
            // 设置使用状态
            groupData.AllPortraitsUseStatus.TryAdd(portraitPath, new());
            if (!groupData.AllPortraitsUseStatus[portraitPath].TryAdd(factionId, gender))
                groupData.AllPortraitsUseStatus[portraitPath][factionId] = Gender.All;
        }

        private BitmapImage GetBitmapImage(string file)
        {
            return Dispatcher.Invoke(() =>
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = new StreamReader(file).BaseStream;
                bitmapImage.EndInit();
                return bitmapImage;
            });
        }

        private void CloseBitmapImage(BitmapImage bitmapImage) =>
            bitmapImage?.StreamSource?.Close();

        private ListBoxItem CreateShowPortraitItem(
            string group,
            string factionId,
            string path,
            BitmapImage bitmapImage,
            Gender gender
        )
        {
            return Dispatcher.Invoke(() =>
            {
                var grid = new Grid();
                // 用于显示肖像
                var image = new Image()
                {
                    Source = bitmapImage,
                    VerticalAlignment = VerticalAlignment.Top,
                    // 停留时显示放大的图像
                    ToolTip = new Image()
                    {
                        Width = 256,
                        Height = 256,
                        Source = bitmapImage,
                    },
                };
                grid.Children.Add(image);
                // 用于显示肖像名称
                var lable = new TextBox()
                {
                    IsReadOnly = true,
                    Text = Path.GetFileNameWithoutExtension(path),
                    Style = (Style)Application.Current.Resources["TextBoxBaseStyle"],
                    VerticalAlignment = VerticalAlignment.Bottom,
                    ToolTip = path,
                };
                grid.Children.Add(lable);
                var listBoxItem = new ListBoxItem()
                {
                    Width = 100,
                    Height = 130,
                    Content = grid,
                    Tag = path,
                    Style = (Style)Application.Current.Resources["ListBoxItemBaseStyle"],
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                    Padding = new(0),
                    ContextMenu = CreateShowPortraitContextMenu(group, factionId, path, gender),
                };
                return listBoxItem;
            });
        }

        private ContextMenu CreateShowPortraitContextMenu(
            string group,
            string factionId,
            string path,
            Gender gender
        )
        {
            string fullpath = $"{allGroupDatas[group].BaseDirectory}\\{path}";
            ContextMenu contextMenu = new();
            // 标记菜单项是否被创建
            contextMenu.Tag = false;
            // 被点击时才加载菜单,可以降低内存占用
            contextMenu.Loaded += (s, e) =>
            {
                if (contextMenu.Tag is true)
                    return;
                contextMenu.Style = (Style)Application.Current.Resources["ContextMenuBaseStyle"];
                // 复制路径
                var menuItem = new MenuItem();
                menuItem.Header = "复制路径";
                menuItem.Style = (Style)Application.Current.Resources["MenuItemBaseStyle"];
                menuItem.Click += (s, e) =>
                {
                    Clipboard.SetDataObject(path);
                };
                contextMenu.Items.Add(menuItem);
                // 打开所在文件夹
                menuItem = new MenuItem();
                menuItem.Header = "打开所在文件夹";
                menuItem.Style = (Style)Application.Current.Resources["MenuItemBaseStyle"];
                menuItem.Click += (s, e) =>
                {
                    Utils.OpenDirectoryAndLocateFile(fullpath);
                };
                contextMenu.Items.Add(menuItem);
                // 删除肖像
                menuItem = new MenuItem();
                menuItem.Header = "删除肖像";
                menuItem.Style = (Style)Application.Current.Resources["MenuItemBaseStyle"];
                menuItem.Click += (s, e) =>
                {
                    RemoveSelectedPortraits(nowGroup, nowFaction, gender);
                    RefreshPortraits(nowGroup, nowFaction, gender);
                };
                contextMenu.Items.Add(menuItem);
                // 删除文件
                menuItem = new MenuItem();
                menuItem.Header = "删除肖像文件";
                menuItem.Style = (Style)Application.Current.Resources["MenuItemBaseStyle"];
                menuItem.Click += (s, e) =>
                {
                    DeleteSelectedPortraits(nowGroup, nowFaction, gender);
                    RefreshPortraits(nowGroup, nowFaction);
                    RefreshGroupImagesCount(nowGroup);
                };
                contextMenu.Items.Add(menuItem);

                contextMenu.Tag = true;
            };
            return contextMenu;
        }

        private void RefreshPortraits(string group, string? factionId = null, Gender? gender = null)
        {
            if (string.IsNullOrEmpty(factionId))
            {
                GroupBox_MalePortraits.Header = GroupBox_MalePortraits.Tag;
                ListBox_MalePortraitsList.Items.Clear();
                GroupBox_FemalePortraits.Header = GroupBox_FemalePortraits.Tag;
                ListBox_FemalePortraitsList.Items.Clear();
                return;
            }
            if (gender is Gender.Male)
                MaleSearchPortraits(group, factionId);
            else if (gender is Gender.Female)
                FemaleSearchPortraits(group, factionId);
            else
            {
                MaleSearchPortraits(group, factionId);
                FemaleSearchPortraits(group, factionId);
            }
            RefreshGroupItemsShowCount(group);
            RefreshGroupBoxPortraitCount(group, factionId);
        }

        private void RefreshGroupItemsShowCount(string group, string? factionId = null)
        {
            var groupData = allGroupDatas[group];
            var allFactionPortraits = groupData.AllFactionPortraits;
            if (string.IsNullOrEmpty(factionId))
            {
                foreach (var item in groupData.AllFactionItems)
                    item.Value.Content =
                        $"{item.Value.ToolTip} ({allFactionPortraits[item.Key].MalePortraitsPath.Count},{allFactionPortraits[item.Key].FemalePortraitsPath.Count})";
            }
            else
            {
                var item = groupData.AllFactionItems[factionId];
                item.Content =
                    $"{item.ToolTip} ({allFactionPortraits[factionId].MalePortraitsPath.Count},{allFactionPortraits[factionId].FemalePortraitsPath.Count})";
            }
        }

        private void RefreshGroupBoxPortraitCount(
            string group,
            string factionId,
            Gender gender = Gender.All
        )
        {
            var groupData = allGroupDatas[group];
            var allFactionPortraits = groupData.AllFactionPortraits;
            if (gender is Gender.Male)
                GroupBox_MalePortraits.Header =
                    $"{GroupBox_MalePortraits.Tag} ({allFactionPortraits[factionId].MalePortraitsPath.Count})";
            else if (gender is Gender.Female)
                GroupBox_FemalePortraits.Header =
                    $"{GroupBox_FemalePortraits.Tag} ({allFactionPortraits[factionId].FemalePortraitsPath.Count})";
            else
            {
                GroupBox_MalePortraits.Header =
                    $"{GroupBox_MalePortraits.Tag} ({allFactionPortraits[factionId].MalePortraitsPath.Count})";
                GroupBox_FemalePortraits.Header =
                    $"{GroupBox_FemalePortraits.Tag} ({allFactionPortraits[factionId].FemalePortraitsPath.Count})";
            }
        }

        private void RefreshGroupImagesCount(string group)
        {
            var expander = allGroupDatas[group].Expander;
            expander.Header =
                $"{expander.ToolTip} ({allGroupDatas[group].AllPortraitImages.Count})";
        }

        private void RefreshGroup(string group)
        {
            var groupData = allGroupDatas[group];
            if (groupData.Expander.Content is ListBox listBox)
            {
                listBox.Items.Clear();
                foreach (var item in groupData.AllFactionItems)
                    listBox.Items.Add(item.Value);
                RefreshGroupImagesCount(group);
            }
        }

        private void MaleSearchPortraits(string group, string factionId)
        {
            if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(factionId))
                return;
            var groupData = allGroupDatas[group];
            var text = TextBox_MaleSearchPortraits.Text;
            var factionPortraits = groupData.AllFactionPortraits[factionId];
            ListBox_MalePortraitsList.Items.Clear();
            if (string.IsNullOrEmpty(text))
            {
                foreach (var path in factionPortraits.MalePortraitsPath)
                    PortraitsListAddItem(groupData.AllMalePortraitItems[path], Gender.Male);
            }
            else
            {
                foreach (var path in factionPortraits.MalePortraitsPath)
                    if (
                        factionPortraits.AllPortraitsName[path].Contains(
                            text,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        PortraitsListAddItem(groupData.AllMalePortraitItems[path], Gender.Male);
            }
        }

        private void FemaleSearchPortraits(string group, string factionId)
        {
            if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(factionId))
                return;
            var groupData = allGroupDatas[group];
            var text = TextBox_FemaleSearchPortraits.Text;
            var factionPortraits = groupData.AllFactionPortraits[factionId];
            ListBox_FemalePortraitsList.Items.Clear();
            if (string.IsNullOrEmpty(text))
            {
                foreach (var path in factionPortraits.FemalePortraitsPath)
                    PortraitsListAddItem(groupData.AllFemalePortraitItems[path], Gender.Female);
            }
            else
            {
                foreach (var path in factionPortraits.FemalePortraitsPath)
                    if (
                        factionPortraits.AllPortraitsName[path].Contains(
                            text,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        PortraitsListAddItem(groupData.AllFemalePortraitItems[path], Gender.Female);
            }
        }

        private void PortraitsListAddItem(ListBoxItem listBoxItem, Gender gender)
        {
            if (gender is Gender.Male)
                ListBox_MalePortraitsList.Items.Add(listBoxItem);
            else if (gender is Gender.Female)
                ListBox_FemalePortraitsList.Items.Add(listBoxItem);
        }

        private void RemoveSelectedPortraits(string group, string factionId, Gender gender)
        {
            if (gender is Gender.Male)
            {
                foreach (ListBoxItem item in ListBox_MalePortraitsList.SelectedItems)
                    RemoveMalePortrait(group, factionId, item.Tag.ToString()!);
            }
            else if (gender is Gender.Female)
            {
                foreach (ListBoxItem item in ListBox_FemalePortraitsList.SelectedItems)
                    RemoveFemalePortrait(group, factionId, item.Tag.ToString()!);
            }
        }

        private void RemoveMalePortrait(string group, string factionId, string portraitPath)
        {
            var groupData = allGroupDatas[group];
            var portraitUseStatus = groupData.AllPortraitsUseStatus[portraitPath];
            if (portraitUseStatus.Count == 1 && portraitUseStatus.First().Value is not Gender.All)
            {
                var result = MessageBoxVM.Show(
                    new(
                        $"此肖像为分组 {group} 唯一引用的位置, 请选择操作\n取消: 取消操作 否: 仅解除引用 是: 解除引用并删除文件\n{portraitPath}"
                    )
                    {
                        Button = MessageBoxVM.Button.YesNoCancel,
                        Icon = MessageBoxVM.Icon.Question
                    }
                );
                if (result is MessageBoxVM.Result.Yes)
                {
                    DeletePortrait(group, factionId, portraitPath);
                    return;
                }
                else if (result is MessageBoxVM.Result.Cancel)
                    return;
            }
            else
            {
                if (portraitUseStatus.TryGetValue(factionId, out var gender))
                {
                    if (gender is Gender.All)
                        portraitUseStatus[factionId] = Gender.Female;
                    else
                        portraitUseStatus.Remove(factionId, out var _);
                }
            }
            groupData.AllFactionPortraits[factionId].Remove(portraitPath, Gender.Male);
            Logger.Info(
                $"分组: {group} 势力ID: {GetVanillaFactionI18n(factionId)} 删除男性肖像: {portraitPath}"
            );
        }

        private void RemoveFemalePortrait(string group, string factionId, string portraitPath)
        {
            var groupData = allGroupDatas[group];
            var portraitUseStatus = groupData.AllPortraitsUseStatus[portraitPath];
            if (portraitUseStatus.Count == 1 && portraitUseStatus.First().Value is not Gender.All)
            {
                var result = MessageBoxVM.Show(
                    new(
                        $"此肖像为分组 {group} 唯一引用的位置, 请选择操作\n取消: 取消操作 否: 仅解除引用 是: 解除引用并删除文件\n{portraitPath}"
                    )
                    {
                        Button = MessageBoxVM.Button.YesNoCancel,
                        Icon = MessageBoxVM.Icon.Question
                    }
                );
                if (result is MessageBoxVM.Result.Yes)
                {
                    DeletePortrait(group, factionId, portraitPath);
                    return;
                }
                else if (result is MessageBoxVM.Result.Cancel)
                    return;
            }
            else
            {
                if (portraitUseStatus.TryGetValue(factionId, out var gender))
                {
                    if (gender is Gender.All)
                        portraitUseStatus[factionId] = Gender.Male;
                    else
                        portraitUseStatus.Remove(factionId, out var _);
                }
            }
            groupData.AllFactionPortraits[factionId].Remove(portraitPath, Gender.Female);
            Logger.Info(
                $"分组: {group} 势力ID: {GetVanillaFactionI18n(factionId)} 删除女性肖像: {portraitPath}"
            );
        }

        private void DeleteSelectedPortraits(string group, string factionId, Gender gender)
        {
            IList selectedItems =
                gender is Gender.Male
                    ? ListBox_MalePortraitsList.SelectedItems
                    : ListBox_FemalePortraitsList.SelectedItems;
            StringBuilder itemPath = new();
            foreach (ListBoxItem item in selectedItems)
                itemPath.AppendLine(item.Tag.ToString());
            if (
                MessageBoxVM.Show(
                    new($"删除所选文件,此操作会解除组内的所有引用并删除至回收站,你确定吗?\n所在分组: {group} 删除对象:\n{itemPath}")
                    {
                        Button = MessageBoxVM.Button.YesNo,
                        Icon = MessageBoxVM.Icon.Question
                    }
                ) is MessageBoxVM.Result.No
            )
                return;
            foreach (ListBoxItem item in selectedItems)
                DeletePortrait(group, factionId, item.Tag.ToString()!);
        }

        private void DeletePortrait(string group, string factionId, string portraitPath)
        {
            var groupData = allGroupDatas[group];
            var portraitUseStatus = groupData.AllPortraitsUseStatus[portraitPath];
            if (portraitUseStatus.Count > 1 || portraitUseStatus.First().Value is Gender.All)
            {
                StringBuilder err = new($"{group} 中有其他势力在使用此肖像 确定要删除吗\n");
                foreach (var useStatus in portraitUseStatus)
                    err.AppendLine($"{GetVanillaFactionI18n(useStatus.Key)}: {useStatus.Value}");
                if (
                    MessageBoxVM.Show(
                        new(err.ToString())
                        {
                            Button = MessageBoxVM.Button.YesNo,
                            Icon = MessageBoxVM.Icon.Question
                        }
                    ) is MessageBoxVM.Result.No
                )
                    return;
            }
            CloseBitmapImage(groupData.AllPortraitImages[portraitPath]);
            groupData.AllPortraitImages.Remove(portraitPath, out var _);
            groupData.AllMalePortraitItems.Remove(portraitPath, out var _);
            groupData.AllFemalePortraitItems.Remove(portraitPath, out var _);
            foreach (var useStatus in portraitUseStatus)
                groupData.AllFactionPortraits[useStatus.Key].Remove(portraitPath);
            groupData.AllPortraitsUseStatus.Remove(portraitPath, out var _);
            var basePortraitPath = Path.Combine(groupData.BaseDirectory, portraitPath);
            var fullPath = File.Exists(basePortraitPath)
                ? basePortraitPath
                : Path.Combine(groupData.PMDirectory, portraitPath);
            groupData.AllPlanToDeleteFiles.TryAdd(portraitPath, fullPath);
            Logger.Info(
                $"分组: {group} 势力ID: {GetVanillaFactionI18n(factionId)} 删除计划肖像文件: {portraitPath}"
            );
        }

        private void SaveAllData()
        {
            foreach (var groupData in allGroupDatas)
            {
                // 备份
                BackupGroup(groupData.Key);
                // 保存所有数据
                SaveGroupData(groupData.Key);
                // 删除预定删除的文件
            }
        }

        private void BackupGroup(string group, bool overwrite = false)
        {
            var groupData = allGroupDatas[group];
            if (!Directory.Exists(groupData.PMBackupDirectory))
                Directory.CreateDirectory(groupData.PMBackupDirectory);
            foreach (var factionPortrait in groupData.AllFactionPortraits)
                BackupFaction(factionPortrait.Value, groupData.PMFactionBackupDirectory, overwrite);
        }

        private void BackupFaction(
            FactionPortraits factionPortraits,
            string factionBackupDirectory,
            bool overwrite = false
        )
        {
            if (!Directory.Exists(factionBackupDirectory))
                Directory.CreateDirectory(factionBackupDirectory);
            var destFileName = $"{factionBackupDirectory}\\{factionPortraits.FileName}";
            if (!File.Exists(destFileName) || overwrite is true)
            {
                // 只备份肖像信息
                //new FactionPortraits(
                //    factionPortraits.FileFullName,
                //    factionPortraits.BaseDirectory
                //).SaveTo(destFileName, true);
            }
        }

        private void SaveGroupData(string group)
        {
            var groupData = allGroupDatas[group];
            var allFactionPortraits = groupData.AllFactionPortraits;
            foreach (var factionPortrait in allFactionPortraits)
                factionPortrait.Value.SaveTo(factionPortrait.Value.FileFullName);
        }

        private void DropFile(string file, Gender gender, string? baseDirectory = null)
        {
            if (string.IsNullOrEmpty(nowFaction))
                return;
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
                {
                    var groupData = allGroupDatas[nowGroup];
                    if (!Directory.Exists(groupData.PMPortraitsDirectory))
                        Directory.CreateDirectory(groupData.PMPortraitsDirectory);
                    AddPortrait(nowGroup, nowFaction, gender, file, baseDirectory);
                    return;
                }
            }
            MessageBoxVM.Show(new("必须是128*128的PNG文件") { Icon = MessageBoxVM.Icon.Warning });
        }

        private void AddPortrait(
            string group,
            string factionId,
            Gender gender,
            string file,
            string? baseDirectory = null
        )
        {
            var groupData = allGroupDatas[group];
            var factionPortraits = groupData.AllFactionPortraits[factionId];
            var fileName = Path.GetFileName(file);
            string fileInPM;
            if (string.IsNullOrEmpty(baseDirectory))
                fileInPM = Path.Combine(groupData.PMPortraitsDirectory, fileName);
            else
            {
                // 检测到基目录时要创建基目录
                fileInPM = Path.Combine(groupData.PMPortraitsDirectory, baseDirectory, fileName);
                var fileDirectory = Path.GetDirectoryName(fileInPM)!;
                if (!Directory.Exists(fileDirectory))
                    Directory.CreateDirectory(fileDirectory);
            }
            string filePathInPM = Path.GetRelativePath(
                Path.GetDirectoryName(groupData.PMDirectory)!,
                fileInPM
            );
            if (
                !File.Exists(fileInPM)
                || MessageBoxVM.Show(
                    new("有重复文件,确定覆盖吗")
                    {
                        Button = MessageBoxVM.Button.YesNo,
                        Icon = MessageBoxVM.Icon.Question
                    }
                ) is MessageBoxVM.Result.Yes
            )
                File.Copy(file, fileInPM, true);
            var bitmapImage = GetBitmapImage(fileInPM);
            factionPortraits.Add(filePathInPM, gender);
            groupData.AllPortraitImages.TryAdd(filePathInPM, bitmapImage);
            if (gender is Gender.Male)
                groupData.AllMalePortraitItems.TryAdd(
                    filePathInPM,
                    CreateShowPortraitItem(group, factionId, filePathInPM, bitmapImage, gender)
                );
            else if (gender is Gender.Female)
                groupData.AllFemalePortraitItems.TryAdd(
                    filePathInPM,
                    CreateShowPortraitItem(group, factionId, filePathInPM, bitmapImage, gender)
                );
            if (
                !groupData.AllPortraitsUseStatus.TryAdd(
                    filePathInPM,
                    new() { [factionId] = gender }
                )
            )
                groupData.AllPortraitsUseStatus[filePathInPM][factionId] = Gender.All;
        }

        private void DropDirectory(string baseDirectory, Gender gender)
        {
            ReadDirectory(baseDirectory);
            void ReadDirectory(string directory)
            {
                var dirInfo = new DirectoryInfo(directory);
                foreach (var fileInfo in dirInfo.GetFiles())
                    DropFile(
                        fileInfo.FullName,
                        gender,
                        Path.GetRelativePath(
                            Path.GetDirectoryName(baseDirectory)!,
                            Path.GetDirectoryName(fileInfo.FullName)!
                        )
                    );
                foreach (var directoryInfo in dirInfo.GetDirectories())
                    ReadDirectory(directoryInfo.FullName);
            }
        }
    }
}
