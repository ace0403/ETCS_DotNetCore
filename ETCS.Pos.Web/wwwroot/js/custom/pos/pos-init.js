/**
 * POS startup: wire DOM events and run initial load.
 */
(function (App) {
    const state = App.state;

    function selectedOptionText(ddl) {
        if (!ddl || ddl.selectedIndex < 0) return '';
        const option = ddl.options[ddl.selectedIndex];
        if (!option || !String(option.value || '').trim()) return '';
        return (option.text || '').trim();
    }

    App.header = {
        updateContext() {
            const textEl = document.getElementById('posContextText');
            if (!textEl) return;

            const school = selectedOptionText(document.getElementById('ddlSchool'));
            const terminal = selectedOptionText(document.getElementById('ddlTerminal'));

            if (school && terminal) {
                textEl.textContent = school + ' · ' + terminal;
            } else if (school) {
                textEl.textContent = school + ' · Select terminal';
            } else {
                textEl.textContent = 'Select branch…';
            }
        },

        isSetupOpen() {
            const panel = document.getElementById('posSetupPanel');
            return !!(panel && !panel.hidden);
        },

        setSetupOpen(open) {
            const panel = document.getElementById('posSetupPanel');
            const btn = document.getElementById('btnPosSetup');
            if (!panel || !btn) return;

            panel.hidden = !open;
            btn.setAttribute('aria-expanded', open ? 'true' : 'false');
            btn.classList.toggle('is-open', open);
        },

        closeSetupIfReady() {
            const schoolId = Number(document.getElementById('ddlSchool')?.value || 0);
            const terminal = selectedOptionText(document.getElementById('ddlTerminal'));
            if (schoolId > 0 && terminal) {
                App.header.setSetupOpen(false);
            }
        },

        bind() {
            const btn = document.getElementById('btnPosSetup');
            if (!btn || btn.dataset.bound === '1') return;
            btn.dataset.bound = '1';

            btn.addEventListener('click', () => {
                App.header.setSetupOpen(!App.header.isSetupOpen());
            });

            document.getElementById('ddlSchool')?.addEventListener('change', () => App.header.updateContext());
            document.getElementById('ddlTerminal')?.addEventListener('change', () => App.header.updateContext());
            document.getElementById('txtTerminalIp')?.addEventListener('input', () => App.header.updateContext());

            App.header.updateContext();

            const schoolId = Number(document.getElementById('ddlSchool')?.value || 0);
            if (schoolId <= 0 || App.state.apiOnline !== true) {
                App.header.setSetupOpen(true);
            }
        }
    };

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
                    App.header?.updateContext();
                    return;
                }
                App.cart.clear();
            }

            state.selectedSchoolId = newSchoolId;
            App.header?.updateContext();
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
        App.header.bind();
        bindStaticControls();
        bindSchoolChange();
        document.getElementById('txtSearch')?.addEventListener('input', () => App.catalog.applySearchFilter());

        App.cart.render();
        App.apiStatus.init();
        await App.bridge.refresh();
        App.bridge.startPolling();
        if (App.state.apiOnline === true) {
            await App.catalog.initDefaults();
        } else {
            App.header.updateContext();
            App.header.setSetupOpen(true);
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => App.init());
    } else {
        App.init();
    }
})(window.PosApp);
