/**
 * POS checkout: cashless (iBonus), cash, card, and undo flows.
 */
(function (App) {
    const state = App.state;
    const { apiIsSuccess, apiMessage, getJsonProp, buildLegacyPurchasePayload, makeTransactionId, validateOrderSetup, spendLimitExceeded, spendLimitMessage, parseNumber } = App.helpers;
    const { BridgeClient, PosApiClient } = App.api;

    App.checkout = {
        async checkout() {
            if (state.posBusy) return;
            if (state.cart.length === 0) {
                await App.ui.warning('Cart is empty.');
                return;
            }
            if (!await validateOrderSetup()) {
                return;
            }

            const lines = App.cart.buildPostPurchaseLines();
            if (lines.length === 0) {
                await App.ui.warning('Cart items are missing item codes required for purchase.');
                return;
            }

            await App.ui.runPosAction('Processing payment', 'Please tap card on the reader…', async () => {
                const meta = App.helpers.getTerminalMeta();
                const payable = App.cart.getPayableAmount();
                const transactionId = makeTransactionId();
                const itemCount = App.cart.cartItemCount();
                const legacy = buildLegacyPurchasePayload();

                if (App.config.prePrintReceipt) {
                    await App.cart.printCurrentReceipt();
                }

                const bridgeResult = await BridgeClient.purchase(payable, transactionId, itemCount);
                if (!bridgeResult.ok || !apiIsSuccess(bridgeResult.data)) {
                    await App.ui.error(apiMessage(bridgeResult.data, 'iBonus purchase failed.'));
                    return;
                }

                const customerId = getJsonProp(bridgeResult.data, 'customerId') || '';
                if (!customerId) {
                    await App.ui.error('iBonus did not return a customer ID.');
                    return;
                }

                const spend = await PosApiClient.spendInfo(customerId);
                if (!spend.ok) {
                    await PosApiClient.rollbackSpendLimit({ customerId, amount: payable });
                    await App.ui.error('Unable to resolve student for customer ID.');
                    return;
                }

                if (spendLimitExceeded(spend.data)) {
                    await PosApiClient.rollbackSpendLimit({ customerId, amount: payable });
                    await App.ui.error(spendLimitMessage(spend.data));
                    return;
                }

                const postResult = await PosApiClient.postPurchaseLines({
                    customerId,
                    transactionId,
                    ipAddress: meta.ip,
                    branchCode: legacy.branchCode,
                    lines
                });
                if (!postResult.ok || !apiIsSuccess(postResult.data)) {
                    await PosApiClient.rollbackSpendLimit({ customerId, amount: payable });
                    await App.ui.error(apiMessage(postResult.data, 'POS purchase recording failed.'));
                    return;
                }

                state.lastTransactionId = transactionId;
                state.lastCustomerId = customerId;
                state.lastPayableAmount = payable;
                state.lastItemCount = itemCount;

                await App.ui.success(apiMessage(postResult.data, 'Cashless transaction successful.'));
                try {
                    await App.cart.printCurrentReceipt();
                } catch (printError) {
                    console.warn('Receipt print failed:', printError);
                }
                App.cart.clear(false);
            });
        },

        async undoLast() {
            if (state.posBusy) return;
            if (state.cart.length === 0 && !state.lastTransactionId) {
                await App.ui.warning('No cashless transaction to undo.');
                return;
            }

            const confirmed = await App.ui.confirm('Please show card on the terminal to undo.', 'Undo Cashless');
            if (!confirmed) return;

            await App.ui.runPosAction('Undoing cashless transaction', 'Please wait…', async () => {
                const payable = state.cart.length > 0 ? App.cart.getPayableAmount() : state.lastPayableAmount;
                const itemCount = state.cart.length > 0 ? App.cart.cartItemCount() : (state.lastItemCount || 1);
                const transactionId = state.lastTransactionId;
                if (!transactionId) {
                    await App.ui.warning('No cashless transaction to undo.');
                    return;
                }

                const bridgeResult = await BridgeClient.undo(payable, transactionId, itemCount);
                if (!bridgeResult.ok || !apiIsSuccess(bridgeResult.data)) {
                    await App.ui.error(apiMessage(bridgeResult.data, 'iBonus undo failed.'));
                    return;
                }

                const totals = App.cart.getTotals();
                await BridgeClient.printUndoReceipt({
                    items: state.cart.length > 0
                        ? state.cart.map(c => ({ name: c.name, price: c.price, quantity: c.quantity }))
                        : [],
                    total: payable,
                    vatPercent: totals.vatPercent,
                    discountPercent: totals.discountPercent,
                    discountApplied: state.discountApplied
                });

                state.lastTransactionId = '';
                state.lastCustomerId = '';
                state.lastPayableAmount = 0;
                state.lastItemCount = 0;
                await App.ui.success('Undo cashless transaction successful.');
                App.cart.clear(false);
            });
        },

        async cashCheckout() {
            if (state.posBusy) return;
            if (state.cart.length === 0) {
                await App.ui.warning('Please select any item to purchase.');
                return;
            }
            if (!await validateOrderSetup()) {
                return;
            }

            await App.ui.runPosAction('Processing cash payment', 'Please wait…', async () => {
                const legacy = buildLegacyPurchasePayload();
                const payable = App.cart.getPayableAmount();
                const result = await PosApiClient.cashPurchase({
                    customerId: 'CASH',
                    amount: payable,
                    branchCode: legacy.branchCode,
                    terminalCode: legacy.terminalCode,
                    terminalCodeNumeric: legacy.terminalCodeNumeric,
                    transactionId: makeTransactionId(),
                    description: 'Cash Purchase'
                });
                if (!result.ok || !apiIsSuccess(result.data)) {
                    await App.ui.error(apiMessage(result.data, 'Cash transaction failed.'));
                    return;
                }
                await App.ui.success(apiMessage(result.data, 'Cash transaction successful.'));
                try {
                    await App.cart.printCurrentReceipt();
                } catch (printError) {
                    console.warn('Receipt print failed:', printError);
                }
                App.cart.clear();
            });
        },

        async undoCashCheckout() {
            if (state.posBusy) return;
            if (!await validateOrderSetup()) {
                return;
            }

            const amount = parseNumber(document.getElementById('txtUndoCashAmount')?.value, 0);
            if (amount <= 0) {
                await App.ui.warning('Enter the amount to refund.');
                return;
            }

            await App.ui.runPosAction('Undoing cash transaction', 'Please wait…', async () => {
                const legacy = buildLegacyPurchasePayload();
                const result = await PosApiClient.undoCashPurchase({
                    customerId: 'CASH',
                    amount,
                    branchCode: legacy.branchCode,
                    terminalCode: legacy.terminalCode,
                    terminalCodeNumeric: legacy.terminalCodeNumeric,
                    transactionId: makeTransactionId(),
                    description: 'Undo Cash Purchase'
                });
                if (!result.ok || !apiIsSuccess(result.data)) {
                    await App.ui.error(apiMessage(result.data, 'Undo cash transaction failed.'));
                    return;
                }
                document.getElementById('txtUndoCashAmount').value = '';
                await App.ui.success('Undo cash transaction successful.');
            });
        },

        async cardCheckout() {
            if (state.posBusy) return;
            if (state.cart.length === 0) {
                await App.ui.warning('Cart is empty.');
                return;
            }
            if (!await validateOrderSetup()) {
                return;
            }

            const creditCardNumber = await App.ui.promptText('Credit / Debit Card', 'Card number');
            if (!creditCardNumber) return;

            await App.ui.runPosAction('Processing card payment', 'Please wait…', async () => {
                const legacy = buildLegacyPurchasePayload();
                const payable = App.cart.getPayableAmount();
                const result = await PosApiClient.cardPurchase({
                    customerId: '',
                    creditCardNumber,
                    amount: payable,
                    branchCode: legacy.branchCode,
                    terminalCode: legacy.terminalCode,
                    terminalCodeNumeric: legacy.terminalCodeNumeric,
                    transactionId: makeTransactionId(),
                    description: 'Card Purchase'
                });
                if (!result.ok || !apiIsSuccess(result.data)) {
                    await App.ui.error(apiMessage(result.data, 'Card purchase failed.'));
                    return;
                }
                await App.ui.success(apiMessage(result.data, 'Card transaction successful.'));
                try {
                    await App.cart.printCurrentReceipt();
                } catch (printError) {
                    console.warn('Receipt print failed:', printError);
                }
                App.cart.clear();
            });
        }
    };
})(window.PosApp);
