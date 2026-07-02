/**
 * POS startup: wire DOM events and run initial load.
 */
(function (App) {
    const state = App.state;

    function bindStaticControls() {
        document.getElementById('btnPay')?.addEventListener('click', () => App.checkout.checkout());
        document.getElementById('btnCash')?.addEventListener('click', () => App.checkout.cashCheckout());
        document.getElementById('btnUndo')?.addEventListener('click', () => App.checkout.undoLast());
        document.getElementById('btnUndoCash')?.addEventListener('click', () => App.checkout.undoCashCheckout());
        document.getElementById('btnReset')?.addEventListener('click', () => App.cart.clear(true));
        document.getElementById('btnRemoveSelected')?.addEventListener('click', () => App.cart.removeSelected());
        document.getElementById('btnApplyDiscount')?.addEventListener('click', () => App.cart.toggleDiscount());
        document.getElementById('btnCard')?.addEventListener('click', () => App.checkout.cardCheckout());

        document.getElementById('txtDiscountPercent')?.addEventListener('input', () => App.cart.onDiscountPercentInput());
        document.getElementById('txtVatPercent')?.addEventListener('input', () => App.cart.refreshTotals());
        document.getElementById('txtReceivedCash')?.addEventListener('input', () => App.cart.updateReturnCash());

        document.getElementById('bridgeStatus')?.addEventListener('click', () => App.bridge.refresh({ showChecking: true }));
        App.bridge.bindOverlayRetry();
    }

    function bindSchoolChange() {
        document.getElementById('ddlSchool')?.addEventListener('change', async function () {
            const ddl = this;
            const newSchoolId = Number(ddl.value || 0);
            const newSchoolName = ddl.options[ddl.selectedIndex]?.text?.trim() || '';

            if (state.cart.length > 0 && state.cartSchoolId > 0 && newSchoolId > 0 && state.cartSchoolId !== newSchoolId) {
                const confirmed = await App.catalog.confirmSchoolSwitch(state.cartSchoolName || 'another school', newSchoolName);
                if (!confirmed) {
                    ddl.value = String(state.cartSchoolId);
                    return;
                }
                App.cart.clear();
            }

            state.selectedSchoolId = newSchoolId;
            if (state.selectedSchoolId > 0) {
                App.catalog.loadTerminals(state.selectedSchoolId);
                App.catalog.loadCategories(state.selectedSchoolId);
            }
        });
    }

    App.init = async function () {
        App.bridge.captureOverlayTemplate();
        App.bridge.watchOverlay();
        App.bridge.updateOverlay();
        App.ui.syncInteractionState();
        App.catalog.bindSchoolSwitchModal();
        bindStaticControls();
        bindSchoolChange();
        document.getElementById('txtSearch')?.addEventListener('input', () => App.catalog.applySearchFilter());

        App.cart.render();
        App.apiStatus.init();
        await App.bridge.refresh();
        App.bridge.startPolling();
        if (App.state.apiOnline === true) {
            await App.catalog.initDefaults();
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => App.init());
    } else {
        App.init();
    }
})(window.PosApp);
