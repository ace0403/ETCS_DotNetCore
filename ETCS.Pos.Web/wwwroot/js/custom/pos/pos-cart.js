/**
 * POS cart: line items, totals, discount, and receipt printing.
 */
(function (App) {
    const state = App.state;
    const { formatMoney, parseNumber } = App.helpers;

    function getDiscountPercent() {
        const raw = document.getElementById('txtDiscountPercent')?.value ?? '';
        if (String(raw).trim() === '') return 0;
        return parseNumber(raw, 0);
    }

    function getVatPercent() {
        const raw = document.getElementById('txtVatPercent')?.value ?? '';
        return parseNumber(raw, App.config.vatPercent || 0);
    }

    function lineTotal(item) {
        let sum = item.price * item.quantity;
        if (state.discountApplied) {
            sum -= sum * getDiscountPercent() / 100;
        }
        return sum;
    }

    function getPayableAmount() {
        return state.cart.reduce((sum, item) => sum + lineTotal(item), 0);
    }

    function getTotals() {
        const subtotal = state.cart.reduce((sum, item) => sum + item.price * item.quantity, 0);
        const discountPercent = state.discountApplied ? getDiscountPercent() : 0;
        const vatPercent = getVatPercent();
        const discountAmount = subtotal * discountPercent / 100;
        const afterDiscount = getPayableAmount();
        const vatAmount = afterDiscount * vatPercent / 100;
        const grandTotal = afterDiscount + vatAmount;
        return { subtotal, discountPercent, vatPercent, discountAmount, afterDiscount, grandTotal };
    }

    function cartItemCount() {
        return state.cart.reduce((sum, item) => sum + item.quantity, 0);
    }

    function buildPostPurchaseLines() {
        return state.cart.map(item => ({
            skuCode: String(item.itemCode || '').trim(),
            amount: lineTotal(item)
        })).filter(line => line.skuCode && line.amount > 0);
    }

    function updateReturnCash() {
        const received = parseNumber(document.getElementById('txtReceivedCash')?.value, 0);
        const payable = getTotals().afterDiscount;
        const change = received - payable;
        const el = document.getElementById('txtReturnCash');
        if (el) el.value = formatMoney(change > 0 ? change : 0);
    }

    function syncDiscountButton() {
        const btn = document.getElementById('btnApplyDiscount');
        if (!btn) return;
        btn.textContent = state.discountApplied ? 'Discount Applied' : 'Apply Discount';
        btn.classList.toggle('is-applied', state.discountApplied);
        btn.disabled = state.discountApplied;
    }

    function bindCartLineEvents() {
        document.querySelectorAll('[data-qty-action]').forEach(btn => {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                App.cart.changeQuantity(Number(btn.dataset.itemId), btn.dataset.qtyAction === 'increase' ? 1 : -1);
            });
        });
        document.querySelectorAll('[data-select-id]').forEach(btn => {
            btn.addEventListener('click', function () {
                state.selectedCartItemId = Number(btn.dataset.selectId);
                App.cart.render();
            });
        });
    }

    function updateTotalsDisplay() {
        const totals = getTotals();
        const count = cartItemCount();

        const setText = (id, prefix, value) => {
            const el = document.getElementById(id);
            if (el) el.textContent = prefix + formatMoney(value);
        };

        setText('txtSubtotal', 'AED ', totals.subtotal);
        setText('txtTotal', 'AED ', totals.afterDiscount);
        setText('txtDiscountAmount', 'AED ', totals.discountAmount);
        updateReturnCash();

        const badgeEl = document.getElementById('orderBadge');
        if (badgeEl) badgeEl.textContent = String(count);

        const countEl = document.getElementById('cartItemCount');
        if (countEl) countEl.textContent = count === 1 ? '1 item' : count + ' items';
    }

    App.cart = {
        getDiscountPercent,
        getVatPercent,
        lineTotal,
        getPayableAmount,
        getTotals,
        cartItemCount,
        buildPostPurchaseLines,
        updateReturnCash,
        syncDiscountButton,

        refreshTotals() {
            updateTotalsDisplay();
        },

        async printCurrentReceipt() {
            const totals = getTotals();
            const result = await App.api.BridgeClient.printReceipt({
                items: state.cart.map(c => ({ name: c.name, price: c.price, quantity: c.quantity })),
                total: totals.afterDiscount,
                vatPercent: totals.vatPercent,
                discountPercent: totals.discountPercent,
                discountApplied: state.discountApplied
            });
            return result;
        },

        render() {
            const container = document.getElementById('cartLines');
            if (!container) return;

            if (state.cart.length === 0) {
                state.selectedCartItemId = 0;
                container.innerHTML = `
                <div class="pos-ticket-empty">
                    <div class="pos-ticket-empty-icon" aria-hidden="true">🛒</div>
                    <p>Order is empty</p>
                    <span>Tap a menu item to add it</span>
                </div>`;
            } else {
                container.innerHTML = state.cart.map(item => `
                <article class="pos-line ${item.id === state.selectedCartItemId ? 'is-selected' : ''}" data-item-id="${item.id}">
                    <div class="pos-line-grid">
                        <span class="pos-line-code">${item.itemCode || item.id}</span>
                        <button type="button" class="pos-line-select" data-select-id="${item.id}">
                            <strong>${item.name}</strong>
                            <span>AED ${formatMoney(item.price)} · AED ${formatMoney(item.price * item.quantity)}</span>
                        </button>
                        <div class="pos-qty-control">
                            <button type="button" class="pos-qty-btn" data-qty-action="decrease" data-item-id="${item.id}" aria-label="Decrease quantity">&#8722;</button>
                            <span class="pos-qty-value">${item.quantity}</span>
                            <button type="button" class="pos-qty-btn pos-qty-btn--accent" data-qty-action="increase" data-item-id="${item.id}" aria-label="Increase quantity">+</button>
                        </div>
                    </div>
                </article>`).join('');
            }

            updateTotalsDisplay();
            bindCartLineEvents();
        },

        addToCart(item) {
            const existing = state.cart.find(c => c.id === item.id);
            if (existing) {
                existing.quantity += 1;
            } else {
                state.cart.push({
                    id: item.id,
                    itemCode: item.itemCode || String(item.id),
                    name: item.name,
                    price: item.price,
                    image: item.image || '',
                    quantity: 1
                });
            }
            state.selectedCartItemId = item.id;
            App.cart.render();
        },

        changeQuantity(itemId, delta) {
            if (App.state.bridgeOnline !== true) {
                App.bridge.requireOnline();
                return;
            }
            const line = state.cart.find(c => c.id === itemId);
            if (!line) return;
            line.quantity += delta;
            if (line.quantity <= 0) {
                state.cart.splice(state.cart.findIndex(c => c.id === itemId), 1);
                if (state.selectedCartItemId === itemId) {
                    state.selectedCartItemId = state.cart.length ? state.cart[0].id : 0;
                }
            }
            App.cart.render();
        },

        removeSelected() {
            if (App.state.bridgeOnline !== true) {
                App.bridge.requireOnline();
                return;
            }
            if (!state.selectedCartItemId) {
                App.ui.warning('Select an item in the order list first.');
                return;
            }
            const idx = state.cart.findIndex(c => c.id === state.selectedCartItemId);
            if (idx >= 0) state.cart.splice(idx, 1);
            state.selectedCartItemId = state.cart.length ? state.cart[state.cart.length - 1].id : 0;
            App.cart.render();
        },

        clear(resetUndoState) {
            if (App.state.bridgeOnline !== true) {
                App.bridge.requireOnline();
                return;
            }
            state.cart.length = 0;
            state.cartSchoolId = 0;
            state.cartSchoolName = '';
            state.selectedCartItemId = 0;
            state.discountApplied = false;
            const discountInput = document.getElementById('txtDiscountPercent');
            if (discountInput) discountInput.value = '';
            const received = document.getElementById('txtReceivedCash');
            const undoCash = document.getElementById('txtUndoCashAmount');
            if (received) received.value = '';
            if (undoCash) undoCash.value = '';
            if (resetUndoState !== false) {
                state.lastTransactionId = '';
                state.lastCustomerId = '';
                state.lastPayableAmount = 0;
                state.lastItemCount = 0;
            }
            updateReturnCash();
            syncDiscountButton();
            App.cart.render();
        },

        onDiscountPercentInput() {
            if (state.discountApplied) {
                updateTotalsDisplay();
            }
        },

        async toggleDiscount() {
            if (!await App.bridge.requireOnline()) return;
            if (state.discountApplied) return;

            const pct = getDiscountPercent();
            if (pct <= 0) {
                await App.ui.warning('Enter a discount percentage first.');
                return;
            }

            const subtotal = state.cart.reduce((sum, item) => sum + item.price * item.quantity, 0);
            if (subtotal <= 0) {
                await App.ui.warning('Add items before applying a discount.');
                return;
            }

            state.discountApplied = true;
            syncDiscountButton();
            updateTotalsDisplay();
            await App.ui.success('Discount applied successfully.', 'Discount');
        }
    };
})(window.PosApp);
