using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.Chemistry.UI
{
    /// <summary>
    /// Initializes a <see cref="ChemMasterWindow"/> and updates it when new server messages are received.
    /// </summary>
    [UsedImplicitly]
    public sealed class ChemMasterBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey) //ADT-Tweak
    {
        [ViewVariables]
        private ChemMasterWindow? _window;

        /// <summary>
        /// Called each time a chem master UI instance is opened. Generates the window and fills it with
        /// relevant info. Sets the actions for static buttons.
        /// </summary>
        protected override void Open()
        {
            base.Open();

            // Setup window layout/elements
            _window = this.CreateWindow<ChemMasterWindow>();
            _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

            // Setup static button actions.
            _window.InputEjectButton.OnPressed += _ => SendPredictedMessage(
                new ItemSlotButtonPressedEvent(SharedChemMaster.InputSlotName));
            _window.BufferTransferButton.OnPressed += _ => SendPredictedMessage(
                new ChemMasterSetModeMessage(ChemMasterMode.Transfer));
            _window.BufferDiscardButton.OnPressed += _ => SendPredictedMessage(
                new ChemMasterSetModeMessage(ChemMasterMode.Discard));
            _window.CreatePillButton.OnPressed += _ => HandleCreatePillPressed();
            _window.CreateBottleButton.OnPressed += _ => HandleCreateBottlePressed();

            for (uint i = 0; i < _window.PillTypeButtons.Length; i++)
            {
                var pillType = i;
                _window.PillTypeButtons[i].OnPressed += _ => SendPredictedMessage(new ChemMasterSetPillTypeMessage(pillType));
            }
            // Transfer buttons
            _window.OnReagentButtonPressed += (_, button, amount, isOutput) => SendPredictedMessage(new ChemMasterReagentAmountButtonMessage(button.Id, amount, button.IsBuffer, isOutput));
            _window.OnSortMethodChanged += sortMethod => SendPredictedMessage(new ChemMasterSortMethodUpdated(sortMethod));
            _window.OnTransferAmountChanged += amount => SendPredictedMessage(new ChemMasterTransferringAmountUpdated(amount));
            _window.OnUpdateAmounts += amounts => SendPredictedMessage(new ChemMasterAmountsUpdated(amounts));
            _window.OnTransferAllPressed += (reagent, isBuffer, isOutput) => SendPredictedMessage(new ChemMasterReagentAmountButtonMessage(reagent, int.MaxValue, isBuffer, isOutput));
            _window.OnToggleBottleFillPressed += slot => SendPredictedMessage(new ChemMasterToggleBottleFillMessage(slot));
            _window.OnBottleSlotEjectPressed += slot => SendPredictedMessage(new ItemSlotButtonPressedEvent($"bottleSlot{slot}"));
            _window.OnRowEjectPressed += row => SendPredictedMessage(new ChemMasterRowEjectMessage(row));
            _window.OnPillContainerSlotSelected += slot => SendPredictedMessage(new ChemMasterSelectPillContainerSlotMessage(slot));
            _window.OnPillCanisterSelected += canisterIndex => SendPredictedMessage(new ChemMasterSelectPillCanisterForCreationMessage(canisterIndex));
            _window.OnPillCanisterEjected += canisterIndex => SendPredictedMessage(new ItemSlotButtonPressedEvent($"pillContainerSlot{canisterIndex}"));
            _window.OnSelectReagentAmount += (reagent, amount) => SendPredictedMessage(new ChemMasterSelectReagentAmountMessage(reagent, amount));
            _window.OnRemoveReagentAmount += (reagent, amount) => SendPredictedMessage(new ChemMasterRemoveReagentAmountMessage(reagent, amount));
            _window.OnTransferReagentFromBottle += (reagent, amount) => SendPredictedMessage(new ChemMasterReagentAmountButtonMessage(reagent, amount, false, false));
        }

        /// <summary>
        /// Update the ui each time new state data is sent from the server.
        /// </summary>
        /// <param name="state">
        /// Data of the <see cref="SharedReagentDispenserComponent"/> that this ui represents.
        /// Sent from the server.
        /// </param>
        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            var castState = (ChemMasterBoundUserInterfaceState) state;
            _window?.UpdateState(castState); // Update window state
        }
        private void HandleCreatePillPressed()
        {
            if (_window == null) return;
            var pillLabel = _window.GeneratePillLabel();
            SendPredictedMessage(new ChemMasterCreatePillsMessage(
                (uint)_window.PillDosage.Value,
                (uint)_window.PillNumber.Value,
                pillLabel));
        }

        private void HandleCreateBottlePressed()
        {
            if (_window == null) return;
            var bottleLabel = _window.GenerateBottleLabel();
            SendPredictedMessage(new ChemMasterOutputToBottleMessage(
                (uint)_window.BottleDosage.Value,
                (uint)_window.BottleNumber.Value,
                bottleLabel));
        }
    }
}
