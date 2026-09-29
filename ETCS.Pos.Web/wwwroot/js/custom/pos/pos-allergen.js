/**
 * POS allergen evaluation and consent for cashless checkout.
 */
(function (App) {
    function escapeHtml(value) {
        return String(value || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function uniqueNames(list) {
        var out = [];
        (list || []).forEach(function (name) {
            var trimmed = String(name || '').trim();
            if (!trimmed) return;
            if (out.some(function (x) { return x.toLowerCase() === trimmed.toLowerCase(); })) return;
            out.push(trimmed);
        });
        return out;
    }

    function buildContainsMap(conflicts) {
        var map = {};
        (conflicts || []).forEach(function (c) {
            var id = Number(App.helpers.getJsonProp(c, 'mealItemId'));
            if (!id) return;
            var names = uniqueNames(App.helpers.getJsonProp(c, 'allergenNames') || []);
            if (names.length) {
                map[id] = 'Contains: ' + names.join(', ');
            }
        });
        return map;
    }

    function containsName(haystack, needle) {
        return String(haystack || '').toLowerCase().indexOf(String(needle || '').toLowerCase()) >= 0;
    }

    function resolveToneClass(name) {
        var n = String(name || '');
        if (containsName(n, 'milk')) return 'is-milk';
        if (containsName(n, 'butter')) return 'is-butter';
        if (containsName(n, 'cheese') || containsName(n, 'dairy')) return 'is-dairy';
        if (containsName(n, 'nut') || containsName(n, 'peanut') || containsName(n, 'almond')) return 'is-nut';
        if (containsName(n, 'egg')) return 'is-egg';
        if (containsName(n, 'wheat') || containsName(n, 'gluten') || containsName(n, 'bread')) return 'is-gluten';
        if (containsName(n, 'fish') || containsName(n, 'seafood') || containsName(n, 'shellfish')) return 'is-fish';
        if (containsName(n, 'soy') || containsName(n, 'soya')) return 'is-soy';
        return 'is-default';
    }

    function resolveTablerIconClass(name) {
        var n = String(name || '');
        if (containsName(n, 'milk')) return 'ti ti-bottle';
        if (containsName(n, 'butter')) return 'ti ti-droplet';
        if (containsName(n, 'cheese') || containsName(n, 'dairy')) return 'ti ti-cheese';
        if (containsName(n, 'nut') || containsName(n, 'peanut') || containsName(n, 'almond')) return 'ti ti-nut';
        if (containsName(n, 'egg')) return 'ti ti-egg';
        if (containsName(n, 'wheat') || containsName(n, 'gluten') || containsName(n, 'bread')) return 'ti ti-bread';
        if (containsName(n, 'fish') || containsName(n, 'seafood') || containsName(n, 'shellfish')) return 'ti ti-fish';
        if (containsName(n, 'soy') || containsName(n, 'soya')) return 'ti ti-leaf';
        if (containsName(n, 'sesame')) return 'ti ti-grain';
        return 'ti ti-alert-circle';
    }

    function renderAllergenChipHtml(name, usePortalStyle) {
        var safe = escapeHtml(name);
        var tone = resolveToneClass(name);
        var iconClass = resolveTablerIconClass(name);
        if (usePortalStyle) {
            return '<span class="etcs-allergen-chip">' + safe + '</span>';
        }
        return '<span class="pos-ingredient-chip ' + tone + '"><i class="' + iconClass + '" aria-hidden="true"></i>' + safe + '</span>';
    }

    function buildContextFromEvaluate(data) {
        if (!data) return null;
        var conflicts = App.helpers.getJsonProp(data, 'conflicts') || [];
        var summary = uniqueNames(App.helpers.getJsonProp(data, 'cartAllergenSummary') || []);
        return {
            customerId: String(App.helpers.getJsonProp(data, 'customerId') || '').trim(),
            studentId: App.helpers.getJsonProp(data, 'studentId'),
            studentName: String(App.helpers.getJsonProp(data, 'studentName') || 'Student').trim() || 'Student',
            registeredAllergens: uniqueNames(App.helpers.getJsonProp(data, 'registeredAllergens') || []),
            cartAllergenSummary: summary,
            hasConflict: App.helpers.getJsonProp(data, 'hasConflict') === true,
            lineContainsByMealItemId: buildContainsMap(conflicts)
        };
    }

    var receiptNoticeFooter = 'Guardian/student acknowledged allergen risk before this meal was served.';

    App.allergen = {
        receiptNoticeFooter,

        getCartMealItemIds() {
            return App.state.cart
                .map(function (item) { return Number(item.id); })
                .filter(function (id) { return id > 0; });
        },

        getCartDeclaredAllergenNames() {
            var names = [];
            (App.state.cart || []).forEach(function (line) {
                (line.declaredAllergens || []).forEach(function (name) {
                    names.push(name);
                });
            });
            return uniqueNames(names);
        },

        async evaluateCheckout(options) {
            var mealItemIds = App.allergen.getCartMealItemIds();
            if (!mealItemIds.length) {
                return { ok: true, data: null };
            }

            var body = { mealItemIds: mealItemIds };
            if (options && options.customerId) {
                body.customerId = options.customerId;
            }
            if (options && options.cardResult) {
                body.cardSn = App.helpers.getJsonProp(options.cardResult, 'cardSn') || '';
                body.uidHex = App.helpers.getJsonProp(options.cardResult, 'uidHex') || '';
                body.uidHexReversed = App.helpers.getJsonProp(options.cardResult, 'uidHexReversed') || '';
                body.uidDecimal = App.helpers.getJsonProp(options.cardResult, 'uidDecimal') || '';
                body.uidDecimalReversed = App.helpers.getJsonProp(options.cardResult, 'uidDecimalReversed') || '';
            }

            return App.api.PosApiClient.evaluateAllergenCheckout(body);
        },

        async showConsentModal(evaluateData) {
            var studentName = escapeHtml(String(App.helpers.getJsonProp(evaluateData, 'studentName') || 'Student').trim() || 'Student');
            var allergens = uniqueNames(App.helpers.getJsonProp(evaluateData, 'cartAllergenSummary') || []);
            var chipsHtml = allergens.length
                ? allergens.map(function (name) { return renderAllergenChipHtml(name, true); }).join('')
                : '<span class="etcs-allergen-chip">Listed allergens</span>';

            var html = ''
                + '<div class="etcs-allergen-dialog">'
                +   '<p class="etcs-allergen-lead">This meal includes items that <strong>' + studentName + '</strong> is sensitive to.</p>'
                +   '<div class="etcs-allergen-section">'
                +     '<div class="etcs-allergen-section-label">Allergens</div>'
                +     '<div class="etcs-allergen-chips">' + chipsHtml + '</div>'
                +   '</div>'
                +   '<p class="etcs-allergen-consent">I consent to <strong>' + studentName + '</strong> receiving this meal despite the allergens it contains.</p>'
                + '</div>';

            var result = await Swal.fire({
                ...App.ui.swalBase(),
                title: 'Allergen warning',
                html: html,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Agree to Serve',
                cancelButtonText: "Don't Add",
                focusCancel: false
            });

            return result.isConfirmed === true;
        },

        async requireCashlessConsent(options) {
            var evalResult = await App.allergen.evaluateCheckout(options);
            if (!evalResult.ok) {
                await App.ui.error('Unable to check allergen information for this student.');
                return null;
            }

            var data = evalResult.data;
            if (!data || App.helpers.getJsonProp(data, 'hasConflict') !== true) {
                return buildContextFromEvaluate(data);
            }

            var agreed = await App.allergen.showConsentModal(data);
            if (!agreed) {
                return null;
            }

            return buildContextFromEvaluate(data);
        },

        applyCashlessContext(context) {
            App.state.lastCashlessAllergenContext = context && context.hasConflict ? context : null;
        },

        clearCashlessContext() {
            App.state.lastCashlessAllergenContext = null;
        },

        renderAllergenChipHtml
    };
})(window.PosApp);
