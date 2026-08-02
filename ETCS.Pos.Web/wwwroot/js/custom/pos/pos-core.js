/**
 * POS core: shared config, state, JSON helpers, terminal helpers, and UI.
 */
window.PosApp = window.PosApp || {};

(function (App) {
    App.config = window.posConfig || {};

    App.state = {
        cart: [],
        selectedSchoolId: 0,
        cartSchoolId: 0,
        cartSchoolName: '',
        selectedCategoryId: 0,
        selectedCartItemId: 0,
        discountApplied: false,
        lastTransactionId: '',
        lastCustomerId: '',
        lastPayableAmount: 0,
        lastItemCount: 0,
        posBusy: false,
        bridgeOnline: null,
        bridgeChecking: true,
        apiOnline: App.config.apiOnline !== false,
        schoolSwitchResolver: null
    };

    App.constants = {
        BRIDGE_POLL_MS: 15000,
        POS_ACTION_BUTTONS: [
            'btnPay', 'btnCard', 'btnUndo', 'btnUndoCash', 'btnCash',
            'btnReset', 'btnRemoveSelected', 'btnApplyDiscount'
        ]
    };

    window.posImageFallback = function (img) {
        if (!img || img.dataset.fallbackApplied === '1') {
            img?.classList.add('is-fallback');
            return;
        }

        const defaultSrc = img.dataset.defaultSrc || App.config.defaultItemImage || '/images/meal-default.png';
        img.dataset.fallbackApplied = '1';
        img.src = defaultSrc;
        img.classList.add('is-fallback');
    };

    App.helpers = {
        getJsonProp(obj, camelKey) {
            if (!obj) return undefined;
            if (Object.prototype.hasOwnProperty.call(obj, camelKey) && obj[camelKey] !== undefined) {
                return obj[camelKey];
            }
            const pascalKey = camelKey.charAt(0).toUpperCase() + camelKey.slice(1);
            return obj[pascalKey];
        },

        apiIsSuccess(data) {
            return App.helpers.getJsonProp(data, 'isSuccess') === true;
        },

        apiMessage(data, fallback) {
            const msg = App.helpers.getJsonProp(data, 'message');
            return msg && String(msg).trim() ? String(msg).trim() : fallback;
        },

        async parseApiResponse(res) {
            let data = null;
            try {
                data = await res.json();
            } catch {
                data = null;
            }
            return { ok: res.ok, data };
        },

        parseNumber(value, fallback) {
            const n = Number(String(value ?? '').replace(',', '.').trim());
            return Number.isFinite(n) ? n : fallback;
        },

        formatMoney(value) {
            return Number(value || 0).toFixed(2);
        },

        getTerminalIp() {
            return document.getElementById('txtTerminalIp')?.value?.trim() || '';
        },

        getTerminalMeta() {
            const ddl = document.getElementById('ddlTerminal');
            const option = ddl?.options[ddl.selectedIndex];
            const numericRaw = option?.dataset?.terminalNumeric || '';
            const numeric = parseInt(numericRaw, 10);
            return {
                code: ddl?.value?.trim() || '',
                ip: option?.dataset?.ipAddress?.trim() || App.helpers.getTerminalIp(),
                numeric: Number.isFinite(numeric) && numeric > 0 ? numeric : 0,
                branchCode: option?.dataset?.branchCode?.trim() || App.config.defaultBranchCode || '1'
            };
        },

        getTerminalCode() {
            return App.helpers.getTerminalMeta().code;
        },

        buildLegacyPurchasePayload() {
            const meta = App.helpers.getTerminalMeta();
            return {
                branchCode: meta.branchCode,
                terminalCode: meta.code,
                terminalCodeNumeric: meta.numeric
            };
        },

        getSelectedSchoolId() {
            const ddl = document.getElementById('ddlSchool');
            const fromDropdown = Number(ddl?.value || 0);
            return fromDropdown > 0 ? fromDropdown : App.state.selectedSchoolId;
        },

        async validateOrderSetup() {
            const schoolId = App.helpers.getSelectedSchoolId();
            if (!schoolId) {
                await App.ui.warning('Select a school before placing an order.', 'School required');
                document.getElementById('ddlSchool')?.focus();
                return false;
            }

            const terminalCode = App.helpers.getTerminalCode();
            if (!terminalCode) {
                await App.ui.warning('Select a terminal before placing an order.', 'Terminal required');
                document.getElementById('ddlTerminal')?.focus();
                return false;
            }

            const terminalIp = App.helpers.getTerminalIp();
            if (!terminalIp) {
                await App.ui.warning('Reader IP is required. Select a terminal with an IP address or enter the reader IP.', 'Reader IP required');
                document.getElementById('txtTerminalIp')?.focus();
                return false;
            }

            return true;
        },

        makeTransactionId() {
            const now = new Date();
            const pad = n => String(n).padStart(2, '0');
            const meta = App.helpers.getTerminalMeta();
            const prefix = meta.numeric > 0 ? String(meta.numeric) : meta.code;
            return prefix + pad(now.getDate()) + pad(now.getMonth() + 1) + pad(now.getFullYear() % 100) + pad(now.getMinutes()) + pad(now.getSeconds());
        },

        spendLimitExceeded(spendData) {
            if (!spendData) return false;
            const h = App.helpers;
            return h.getJsonProp(spendData, 'legacyIsDailyLimitExceeded')
                || h.getJsonProp(spendData, 'legacyIsWeeklyLimitExceeded')
                || h.getJsonProp(spendData, 'isDailyLimitExceeded')
                || h.getJsonProp(spendData, 'isWeeklyLimitExceeded');
        },

        spendLimitMessage(spendData) {
            const h = App.helpers;
            if (h.getJsonProp(spendData, 'legacyIsWeeklyLimitExceeded') || h.getJsonProp(spendData, 'isWeeklyLimitExceeded')) {
                return 'Weekly spending limit exceeded!';
            }
            if (h.getJsonProp(spendData, 'legacyIsDailyLimitExceeded') || h.getJsonProp(spendData, 'isDailyLimitExceeded')) {
                return 'Daily spending limit exceeded!';
            }
            return 'Spending limit exceeded!';
        },

        getSelectedSchoolInfo() {
            const ddl = document.getElementById('ddlSchool');
            if (!ddl) return { id: 0, name: '' };
            const id = Number(ddl.value || 0);
            const name = ddl.options[ddl.selectedIndex]?.text?.trim() || '';
            return { id, name };
        }
    };

    App.ui = {
        swalBase() {
            return {
                confirmButtonColor: '#4680ff',
                cancelButtonColor: '#94a3b8',
                customClass: { popup: 'pos-swal' }
            };
        },
        async error(message, title = 'Error') {
            if (typeof Swal.isVisible === 'function' && Swal.isVisible()) {
                Swal.close();
            }
            await Swal.fire({ ...this.swalBase(), icon: 'error', title, text: message });
        },
        async success(message, title = 'Success') {
            if (typeof Swal.isVisible === 'function' && Swal.isVisible()) {
                Swal.close();
            }
            await Swal.fire({
                ...this.swalBase(),
                icon: 'success',
                title,
                text: message,
                timer: 2200,
                timerProgressBar: true,
                showConfirmButton: false
            });
        },
        async warning(message, title = 'Attention') {
            if (typeof Swal.isVisible === 'function' && Swal.isVisible()) {
                Swal.close();
            }
            await Swal.fire({ ...this.swalBase(), icon: 'warning', title, text: message });
        },
        async confirm(message, title = 'Confirm') {
            const result = await Swal.fire({
                ...this.swalBase(),
                icon: 'question',
                title,
                text: message,
                showCancelButton: true,
                confirmButtonText: 'Yes',
                cancelButtonText: 'Cancel'
            });
            return result.isConfirmed;
        },
        async promptText(title, inputLabel) {
            const result = await Swal.fire({
                ...this.swalBase(),
                title,
                input: 'text',
                inputLabel,
                showCancelButton: true,
                confirmButtonText: 'Continue',
                inputValidator: value => (!value || !String(value).trim()) ? 'This field is required' : undefined
            });
            return result.isConfirmed ? String(result.value).trim() : '';
        },
        showLoading(title, text) {
            Swal.fire({
                title,
                text,
                allowOutsideClick: false,
                allowEscapeKey: false,
                showConfirmButton: false,
                didOpen: () => Swal.showLoading()
            });
        },
        hideLoading() {
            if (typeof Swal.isLoading === 'function' && Swal.isLoading()) {
                Swal.close();
            }
        },

        setActionButtonsDisabled(disabled) {
            App.state.posBusy = disabled;
            App.ui.syncInteractionState();
        },

        syncInteractionState() {
            const blockActions = App.state.posBusy
                || App.state.bridgeOnline !== true
                || App.state.apiOnline !== true;
            App.constants.POS_ACTION_BUTTONS.forEach(id => {
                const el = document.getElementById(id);
                if (el) el.disabled = blockActions;
            });
            const ddlSchool = document.getElementById('ddlSchool');
            if (ddlSchool) ddlSchool.disabled = App.state.apiOnline !== true;
            document.body.classList.toggle('pos-is-busy', App.state.posBusy);
            document.body.classList.toggle('pos-bridge-offline', App.state.bridgeOnline !== true);
            document.body.classList.toggle('pos-api-offline', App.state.apiOnline !== true);
            App.bridge.setAppLocked(App.state.bridgeOnline !== true);
        },

        async runPosAction(loadingTitle, loadingText, action) {
            if (App.state.posBusy) return;
            if (!await App.bridge.requireOnline()) return;
            App.state.posBusy = true;
            App.ui.syncInteractionState();
            App.ui.showLoading(loadingTitle, loadingText);
            try {
                await action();
            } catch (error) {
                console.error('POS action failed:', error);
                await App.ui.error(error?.message || 'An unexpected error occurred.');
            } finally {
                if (typeof Swal.isLoading === 'function' && Swal.isLoading()) {
                    Swal.close();
                }
                App.state.posBusy = false;
                App.ui.syncInteractionState();
            }
        }
    };

    App.bridge = {
        pollTimer: null,
        overlayTemplate: null,
        overlayObserver: null,
        blockEventsHandler: null,
        storageKeys: {
            everConnected: 'etcs.bridgeEverConnected',
            lastConnectedAt: 'etcs.bridgeLastConnectedAt'
        },

        isBridgeHealthy(health) {
            if (!health?.ok) return false;
            const status = App.helpers.getJsonProp(health.data, 'status');
            return !status || String(status).toLowerCase() === 'ok';
        },

        updateStatusChip(online, localIp) {
            const el = document.getElementById('bridgeStatus');
            if (!el) return;
            if (online) {
                el.classList.remove('is-offline');
                el.title = 'Bridge OK (' + (localIp || 'localhost') + ') — click to recheck';
                el.setAttribute('aria-label', 'Bridge connected');
            } else if (App.state.bridgeChecking) {
                el.classList.add('is-offline');
                el.title = 'Checking POS Bridge connection';
                el.setAttribute('aria-label', 'Checking bridge');
            } else {
                el.classList.add('is-offline');
                el.title = 'POS Bridge offline — click to check connection';
                el.setAttribute('aria-label', 'Bridge offline');
            }
        },

        hasBridgeEverConnected() {
            try {
                return localStorage.getItem(App.bridge.storageKeys.everConnected) === '1';
            } catch {
                return false;
            }
        },

        markBridgeConnected() {
            try {
                localStorage.setItem(App.bridge.storageKeys.everConnected, '1');
                localStorage.setItem(App.bridge.storageKeys.lastConnectedAt, new Date().toISOString());
            } catch {
                // ignore storage failures (private mode, policy blocks)
            }
        },

        formatLastConnected(iso) {
            if (!iso) return '';
            const date = new Date(iso);
            if (Number.isNaN(date.getTime())) return '';

            const now = new Date();
            const time = date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
            if (date.toDateString() === now.toDateString()) {
                return 'Last connected: today ' + time;
            }

            const yesterday = new Date(now);
            yesterday.setDate(yesterday.getDate() - 1);
            if (date.toDateString() === yesterday.toDateString()) {
                return 'Last connected: yesterday ' + time;
            }

            return 'Last connected: ' + date.toLocaleDateString() + ' ' + time;
        },

        setOverlaySteps(stepsEl, steps) {
            if (!stepsEl) return;
            stepsEl.replaceChildren();
            steps.forEach(text => {
                const item = document.createElement('li');
                item.textContent = text;
                stepsEl.appendChild(item);
            });
        },

        setOverlayButtonStyle(el, isPrimary) {
            if (!el) return;
            el.classList.toggle('pos-bridge-overlay-btn--secondary', !isPrimary);
        },

        orderOverlayActions(actionsEl, primaryEl, secondaryEl) {
            if (!actionsEl) return;
            if (primaryEl) actionsEl.appendChild(primaryEl);
            if (secondaryEl) actionsEl.appendChild(secondaryEl);
        },

        captureOverlayTemplate() {
            const overlay = document.getElementById('bridgeOfflineOverlay');
            if (overlay && !App.bridge.overlayTemplate) {
                App.bridge.overlayTemplate = overlay.outerHTML;
            }
        },

        bindOverlayRetry() {
            const btn = document.getElementById('btnBridgeRetry');
            if (!btn || btn.dataset.bound === '1') return;
            btn.dataset.bound = '1';
            btn.addEventListener('click', () => App.bridge.refresh({ showChecking: true }));
        },

        ensureOverlay() {
            let overlay = document.getElementById('bridgeOfflineOverlay');
            if (overlay) return overlay;
            if (!App.bridge.overlayTemplate) return null;
            document.body.insertAdjacentHTML('beforeend', App.bridge.overlayTemplate);
            App.bridge.bindOverlayRetry();
            return document.getElementById('bridgeOfflineOverlay');
        },

        setAppLocked(locked) {
            const app = document.querySelector('.pos-app');
            const modal = document.getElementById('schoolSwitchModal');
            if (app) {
                if (locked) {
                    app.setAttribute('inert', '');
                    app.setAttribute('aria-hidden', 'true');
                } else {
                    app.removeAttribute('inert');
                    app.removeAttribute('aria-hidden');
                }
            }
            if (modal) {
                if (locked) {
                    modal.setAttribute('inert', '');
                    modal.hidden = true;
                    modal.setAttribute('aria-hidden', 'true');
                } else {
                    modal.removeAttribute('inert');
                    modal.setAttribute('aria-hidden', 'true');
                }
            }
        },

        watchOverlay() {
            App.bridge.captureOverlayTemplate();
            if (App.bridge.overlayObserver) return;

            App.bridge.overlayObserver = new MutationObserver(() => {
                if (App.state.bridgeOnline !== true) {
                    App.bridge.ensureOverlay();
                    App.bridge.updateOverlay();
                }
            });
            App.bridge.overlayObserver.observe(document.body, { childList: true });

            if (App.bridge.blockEventsHandler) return;
            App.bridge.blockEventsHandler = function (event) {
                if (App.state.bridgeOnline === true) return;
                const overlay = document.getElementById('bridgeOfflineOverlay');
                if (overlay && !overlay.classList.contains('is-hidden') && overlay.contains(event.target)) {
                    return;
                }
                event.preventDefault();
                event.stopPropagation();
                App.bridge.ensureOverlay();
                App.bridge.updateOverlay();
            };
            document.addEventListener('click', App.bridge.blockEventsHandler, true);
            document.addEventListener('keydown', App.bridge.blockEventsHandler, true);
        },

        updateOverlay() {
            if (App.state.bridgeOnline !== true) {
                App.bridge.ensureOverlay();
            }

            const overlay = document.getElementById('bridgeOfflineOverlay');
            const title = document.getElementById('bridgeOverlayTitle');
            const subline = document.getElementById('bridgeOverlaySubline');
            const stepsWrap = document.getElementById('bridgeOverlayStepsWrap');
            const stepsEl = document.getElementById('bridgeOverlaySteps');
            const lastConnected = document.getElementById('bridgeOverlayLastConnected');
            const actionsEl = document.getElementById('bridgeOverlayActions');
            const retryBtn = document.getElementById('btnBridgeRetry');
            const downloadBtn = document.getElementById('btnBridgeDownload');
            if (!overlay) return;

            const online = App.state.bridgeOnline === true;
            const checking = App.state.bridgeChecking === true;
            const downloadAvailable = Boolean(App.config.bridgeSetupDownloadUrl);
            const wasConnected = App.bridge.hasBridgeEverConnected();

            if (online) {
                overlay?.classList.add('is-hidden');
                overlay?.setAttribute('aria-hidden', 'true');
                overlay?.setAttribute('aria-busy', 'false');
                document.body.classList.remove('pos-bridge-overlay-open');
                App.bridge.setAppLocked(false);
                return;
            }

            overlay.classList.remove('is-hidden');
            overlay.setAttribute('aria-hidden', 'false');
            document.body.classList.add('pos-bridge-overlay-open');
            App.bridge.setAppLocked(true);

            if (checking) {
                overlay.dataset.mode = 'checking';
                overlay.setAttribute('aria-busy', 'true');
                if (title) title.textContent = 'Connecting to this terminal…';
                if (subline) subline.textContent = 'Waiting for local bridge on this PC.';
                if (stepsWrap) stepsWrap.hidden = true;
                if (lastConnected) lastConnected.hidden = true;
                if (retryBtn) {
                    retryBtn.hidden = false;
                    retryBtn.disabled = true;
                    App.bridge.setOverlayButtonStyle(retryBtn, true);
                    const label = retryBtn.querySelector('.pos-bridge-overlay-btn-label');
                    if (label) label.textContent = 'Checking…';
                }
                if (downloadBtn) {
                    downloadBtn.hidden = true;
                    downloadBtn.setAttribute('aria-hidden', 'true');
                }
                return;
            }

            overlay.dataset.mode = 'offline';
            overlay.setAttribute('aria-busy', 'false');

            const setupMode = !wasConnected;
            if (title) {
                title.textContent = setupMode
                    ? 'Set up this counter terminal'
                    : 'POS Bridge is not running';
            }
            if (subline) {
                subline.textContent = setupMode
                    ? 'Sales run on the server; card reader and printer need the bridge app on this PC.'
                    : 'This PC lost contact with the local bridge. Sales on the server are unaffected.';
            }

            if (stepsWrap) stepsWrap.hidden = false;
            if (setupMode) {
                App.bridge.setOverlaySteps(stepsEl, downloadAvailable
                    ? [
                        'Download and run the bridge setup on this PC.',
                        'Finish the install and reboot if asked.',
                        'Click Check connection.'
                    ]
                    : [
                        'Ask your supervisor to install the POS Bridge on this PC.',
                        'Reboot this PC if asked after install.',
                        'Click Check connection.'
                    ]);
            } else {
                App.bridge.setOverlaySteps(stepsEl, downloadAvailable
                    ? [
                        'Ask your supervisor to restart this PC.',
                        'If still offline, run the bridge setup again.',
                        'Click Check connection.'
                    ]
                    : [
                        'Ask your supervisor to restart this PC.',
                        'If still offline, ask them to check the ETCSPosBridge service.',
                        'Click Check connection.'
                    ]);
            }

            if (lastConnected) {
                if (wasConnected) {
                    let lastConnectedText = '';
                    try {
                        lastConnectedText = App.bridge.formatLastConnected(
                            localStorage.getItem(App.bridge.storageKeys.lastConnectedAt)
                        );
                    } catch {
                        lastConnectedText = '';
                    }
                    if (lastConnectedText) {
                        lastConnected.textContent = lastConnectedText;
                        lastConnected.hidden = false;
                    } else {
                        lastConnected.hidden = true;
                    }
                } else {
                    lastConnected.hidden = true;
                }
            }

            if (retryBtn) {
                retryBtn.hidden = false;
                retryBtn.disabled = false;
                const label = retryBtn.querySelector('.pos-bridge-overlay-btn-label');
                if (label) label.textContent = 'Check connection';
            }

            if (downloadBtn) {
                if (downloadAvailable) {
                    downloadBtn.hidden = false;
                    downloadBtn.setAttribute('aria-hidden', 'false');
                } else {
                    downloadBtn.hidden = true;
                    downloadBtn.setAttribute('aria-hidden', 'true');
                }
            }

            if (setupMode && downloadAvailable) {
                App.bridge.setOverlayButtonStyle(downloadBtn, true);
                App.bridge.setOverlayButtonStyle(retryBtn, false);
                App.bridge.orderOverlayActions(actionsEl, downloadBtn, retryBtn);
            } else {
                App.bridge.setOverlayButtonStyle(retryBtn, true);
                App.bridge.setOverlayButtonStyle(downloadBtn, false);
                App.bridge.orderOverlayActions(actionsEl, retryBtn, downloadAvailable ? downloadBtn : null);
            }
        },

        async refresh(options = {}) {
            const silent = options.silent === true;
            const showChecking = !silent && (options.showChecking === true || App.state.bridgeOnline === null);

            if (showChecking) {
                App.state.bridgeChecking = true;
                App.bridge.updateOverlay();
                App.bridge.updateStatusChip(false, '');
            }

            let online = false;
            let localIp = '';
            try {
                const health = await App.api.BridgeClient.health();
                online = App.bridge.isBridgeHealthy(health);
                localIp = App.helpers.getJsonProp(health.data, 'localIp') || '';
            } catch {
                online = false;
            }

            App.state.bridgeChecking = false;
            App.state.bridgeOnline = online;
            if (online) {
                App.bridge.markBridgeConnected();
            }
            App.bridge.updateStatusChip(online, localIp);
            App.bridge.updateOverlay();
            App.ui.syncInteractionState();
            return online;
        },

        startPolling() {
            if (App.bridge.pollTimer) return;
            const intervalMs = App.config.bridgePollIntervalMs || App.constants.BRIDGE_POLL_MS;
            App.bridge.pollTimer = window.setInterval(
                () => App.bridge.refresh({ silent: true }),
                intervalMs
            );
        },

        async requireOnline() {
            if (App.state.bridgeOnline === true) return true;
            await App.bridge.refresh({ silent: App.state.bridgeOnline === false });
            return App.state.bridgeOnline === true;
        }
    };

    App.apiStatus = {
        reload() {
            window.location.reload();
        },

        bindRetry() {
            document.getElementById('btnApiRetry')?.addEventListener('click', () => App.apiStatus.reload());
            document.getElementById('apiStatus')?.addEventListener('click', () => {
                if (App.state.apiOnline !== true) {
                    App.apiStatus.reload();
                }
            });
        },

        init() {
            App.apiStatus.bindRetry();
            App.ui.syncInteractionState();
        }
    };
})(window.PosApp);
