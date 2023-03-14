using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKW.ViewModels.Controls;
using StarsectorTools.Libs.GameInfo;
using StarsectorTools.Libs.Utils;

namespace StarsectorToolsExtension.PortraitsManager.ViewModels
{
    internal partial class PortraitsManagerViewModel : ObservableObject
    {

        [ObservableProperty]
        private string _maleGroupBoxHeader = "男性肖像";

        [ObservableProperty]
        private string _femaleGroupBoxHeader = "女性肖像";

        [ObservableProperty]
        private string _malePortraitsFilterText = string.Empty;

        partial void OnMalePortraitsFilterTextChanged(string value) => MalePortraitFilter(value);

        [ObservableProperty]
        private string _femalePortraitsFilterText = string.Empty;

        partial void OnFemalePortraitsFilterTextChanged(string value) => FemalePortraitFilter(value);

        [ObservableProperty]
        private bool _isRemindSave = false;

        [ObservableProperty]
        private GroupData _vanillaGroupData = null!;

        [ObservableProperty]
        private ObservableCollection<ListBoxItemVM> _nowShowMalePortraitItems = null!;

        partial void OnNowShowMalePortraitItemsChanged(ObservableCollection<ListBoxItemVM> value)
        {
            if (value is not null)
                value.CollectionChanged += (s, e) => RefreshMaleGroupBoxHeader();
            RefreshMaleGroupBoxHeader();
        }

        private void RefreshMaleGroupBoxHeader()
        {
            MaleGroupBoxHeader = $"男性肖像 ({NowShowMalePortraitItems?.Count})";
        }

        [ObservableProperty]
        private ObservableCollection<ListBoxItemVM> _nowShowFemalePortraitItems = null!;

        partial void OnNowShowFemalePortraitItemsChanged(ObservableCollection<ListBoxItemVM> value)
        {
            if (value is not null)
                value.CollectionChanged += (s, e) => RefreshFemaleGroupBoxHeader();
            RefreshFemaleGroupBoxHeader();
        }

        private void RefreshFemaleGroupBoxHeader()
        {
            FemaleGroupBoxHeader = $"女性肖像 ({NowShowFemalePortraitItems?.Count})";
        }

        private ListBoxItemVM _nowSelectedFactionItem = null!;

        internal List<ListBoxItemVM> NowSelectedMalePortraitItems { get; private set; } = null!;
        internal List<ListBoxItemVM> NowSelectedFemalePortraitItems { get; private set; } = null!;

        public PortraitsManagerViewModel()
        { }

        public PortraitsManagerViewModel(bool noop)
        {
            Instance = this;
            VanillaGroupData = GetGroupData(_StrVanilla, "原版", GameInfo.CoreDirectory)!;
            VanillaGroupData.IsExpanded = true;
        }

        [RelayCommand]
        internal void Save()
        {
            VanillaGroupData.Save();
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