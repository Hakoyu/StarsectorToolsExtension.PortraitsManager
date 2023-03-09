using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKW.Libs.Log4Cs;
using HKW.ViewModels.Controls;
using HKW.ViewModels.Dialogs;
using StarsectorTools.Libs.GameInfo;
using StarsectorTools.Libs.Utils;
using StarsectorToolsExtension.PortraitsManager.Models;

namespace StarsectorToolsExtension.PortraitsManager.ViewModels
{
    internal partial class PortraitsManagerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _maleGroupBoxHeader = "男性肖像";

        [ObservableProperty]
        private string _femaleGroupBoxHeader = "女性肖像";

        [ObservableProperty]
        private string _malePortraitFilterText;

        partial void OnMalePortraitFilterTextChanged(string value) => MalePortraitFilter(value);

        [ObservableProperty]
        private string _femalePortraitFilterText;

        partial void OnFemalePortraitFilterTextChanged(string value) => FemalePortraitFilter(value);

        [ObservableProperty]
        private bool _isRemindSave = false;

        [ObservableProperty]
        ComboBoxVM _comboBox_GroupList =
            new()
            {
                new() { Content = "原版", Tag = _StrVanilla },
                new() { Content = "已启用模组", Tag = nameof(ModTypeGroup.Enabled) },
                new() { Content = "已收藏模组", Tag = nameof(ModTypeGroup.Collected) }
            };

        [ObservableProperty]
        private ObservableCollection<GroupData> _allGroupDatas = new();

        private GroupData _nowGroupData;

        [ObservableProperty]
        private ObservableCollection<ListBoxItemVM> _nowShowMalePortraitItems;

        partial void OnNowShowMalePortraitItemsChanged(ObservableCollection<ListBoxItemVM> value)
        {
            MaleGroupBoxHeader = $"男性肖像 ({NowShowMalePortraitItems.Count})";
        }

        [ObservableProperty]
        private ObservableCollection<ListBoxItemVM> _nowShowFemalePortraitItems;

        partial void OnNowShowFemalePortraitItemsChanged(ObservableCollection<ListBoxItemVM> value)
        {
            FemaleGroupBoxHeader = $"女性肖像 ({NowShowFemalePortraitItems.Count})";
        }

        private ListBoxItemVM _nowSelectedFactionItem;

        internal List<ListBoxItemVM> NowSelectedMalePortraitItems { get; private set; }
        internal List<ListBoxItemVM> NowSelectedFemalePortraitItems { get; private set; }

        public PortraitsManagerViewModel() { }

        public PortraitsManagerViewModel(bool noop)
        {
            Instance = this;
            ComboBox_GroupList.SelectionChangedEvent += ComboBox_GroupList_SelectionChangedEvent;
            ComboBox_GroupList.SelectedIndex = 0;
            InitializeGroup();
        }

        [RelayCommand]
        internal void Save()
        {
            foreach (var groupData in AllGroupDatas)
                groupData.Save();
            IsRemindSave = false;
        }

        [RelayCommand]
        private void MaleSelectionChanged(IList values)
        {
            NowSelectedMalePortraitItems = new(values.OfType<ListBoxItemVM>());
        }

        [RelayCommand]
        private void FemaleSelectionChanged(IList values)
        {
            NowSelectedFemalePortraitItems = new(values.OfType<ListBoxItemVM>());
        }
    }
}
