using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKW.ViewModels.Controls;
using StarsectorTools.Libs.GameInfo;
using StarsectorTools.Libs.Utils;

namespace StarsectorToolsExtension.PortraitsManager.ViewModels
{
    internal partial class AddFactionWindowViewModel : WindowVM
    {
        [ObservableProperty]
        private string _factionName = string.Empty;

        partial void OnFactionNameChanged(string value)
        {
            if (VanillaFactions.AllVanillaFactionsI18n.ContainsKey(value))
            {
                var item = ComboBox_VanillaFaction.SelectedItem;
                if (item is not ComboBoxItemVM || item!.ToolTip!.ToString() != value)
                    ComboBox_VanillaFaction.SelectedItem = ComboBox_VanillaFaction.First(
                        i => i.ToolTip!.ToString()! == value
                    );
            }
            else
                ComboBox_VanillaFaction.SelectedItem = null;
        }

        [ObservableProperty]
        private ComboBoxVM _comboBox_VanillaFaction = new();

        public string OriginalFactionName { get; set; } = string.Empty;

        public GroupData BaseGroupData { get; set; } = null!;

        public AddFactionWindowViewModel() { }

        public AddFactionWindowViewModel(object window)
            : base(window)
        {
            DataContext = this;
            GetAllVanillaFaction();
            ComboBox_VanillaFaction.SelectionChangedEvent +=
                ComboBox_VanillaFaction_SelectionChangedEvent;
            ShowDialogEvent += () => Utils.SetMainWindowBlurEffect();
            HideEvent += () =>
            {
                FactionName = string.Empty;
                OriginalFactionName = string.Empty;
                BaseGroupData = null!;
                Utils.RemoveMainWindowBlurEffect();
            };
        }

        private void ComboBox_VanillaFaction_SelectionChangedEvent(ComboBoxItemVM item)
        {
            if (ComboBox_VanillaFaction.SelectedItem is not null)
                FactionName = ComboBox_VanillaFaction.SelectedItem.ToolTip!.ToString()!;
        }

        private void GetAllVanillaFaction()
        {
            foreach (var faction in VanillaFactions.AllVanillaFactionsI18n)
            {
                ComboBox_VanillaFaction.Add(
                    new() { Content = faction.Value, ToolTip = faction.Key, }
                );
            }
        }

        [RelayCommand]
        private void OK()
        {
            OKEvent?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            CancelEvent?.Invoke();
        }

        public delegate void DelegateHandler();

        public event DelegateHandler? OKEvent;

        public event DelegateHandler? CancelEvent;
    }
}
