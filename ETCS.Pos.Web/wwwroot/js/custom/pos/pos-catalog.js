/**
 * POS catalog: schools, terminals, categories, products, and search.
 */
(function (App) {
    const state = App.state;
    const config = App.config;

    function applySearchFilter() {
        const term = (document.getElementById('txtSearch')?.value || '').trim().toLowerCase();
        document.querySelectorAll('.pos-tile').forEach(tile => {
            const haystack = tile.dataset.search || '';
            tile.classList.toggle('is-hidden', term.length > 0 && !haystack.includes(term));
        });
    }

    function applyTerminalIp() {
        const ddl = document.getElementById('ddlTerminal');
        const ipInput = document.getElementById('txtTerminalIp');
        if (!ddl || !ipInput) return;

        const option = ddl.options[ddl.selectedIndex];
        ipInput.value = option?.dataset?.ipAddress?.trim() || '';
        App.header?.updateContext();
    }

    function selectFirstTerminal() {
        const ddl = document.getElementById('ddlTerminal');
        if (!ddl) return;

        const first = Array.from(ddl.options).find(o => o.value);
        if (first) {
            ddl.value = first.value;
        }
        applyTerminalIp();
    }

    function bindTerminalDropdown() {
        const ddl = document.getElementById('ddlTerminal');
        if (!ddl || ddl.dataset.ipBound === '1') return;

        ddl.dataset.ipBound = '1';
        ddl.addEventListener('change', applyTerminalIp);
        selectFirstTerminal();
    }

    function bindSchoolSwitchModal() {
        const modal = document.getElementById('schoolSwitchModal');
        const btnConfirm = document.getElementById('schoolSwitchConfirm');
        const btnCancel = document.getElementById('schoolSwitchCancel');
        if (!modal || !btnConfirm || !btnCancel) return;

        function closeSchoolSwitch(result) {
            modal.hidden = true;
            modal.setAttribute('aria-hidden', 'true');
            if (state.schoolSwitchResolver) {
                state.schoolSwitchResolver(result);
                state.schoolSwitchResolver = null;
            }
        }

        btnConfirm.addEventListener('click', () => closeSchoolSwitch(true));
        btnCancel.addEventListener('click', () => closeSchoolSwitch(false));
        modal.querySelectorAll('[data-school-switch-dismiss]').forEach(el => {
            el.addEventListener('click', () => closeSchoolSwitch(false));
        });
    }

    function confirmSchoolSwitch(fromName, toName) {
        const modal = document.getElementById('schoolSwitchModal');
        const fromEl = document.getElementById('schoolSwitchFrom');
        const toEl = document.getElementById('schoolSwitchTo');
        if (!modal || !fromEl || !toEl) {
            return App.ui.confirm(
                'Your cart has items from ' + fromName + '. Clear cart and add items from ' + toName + '?',
                'Replace cart items?'
            );
        }

        fromEl.textContent = fromName;
        toEl.textContent = toName;
        modal.hidden = false;
        modal.setAttribute('aria-hidden', 'false');

        return new Promise(resolve => {
            state.schoolSwitchResolver = resolve;
        });
    }

    function bindCategoryButtons() {
        document.querySelectorAll('.pos-category-item').forEach(btn => {
            btn.addEventListener('click', function () {
                document.querySelectorAll('.pos-category-item').forEach(b => {
                    b.classList.remove('is-active');
                    b.setAttribute('aria-selected', 'false');
                });
                btn.classList.add('is-active');
                btn.setAttribute('aria-selected', 'true');
                state.selectedCategoryId = Number(btn.dataset.categoryId || 0);
                if (state.selectedSchoolId > 0 && state.selectedCategoryId >= 0) {
                    App.catalog.loadProducts(state.selectedSchoolId, state.selectedCategoryId);
                }
            });
        });
    }

    function bindProductButtons() {
        document.querySelectorAll('.pos-tile img.pos-tile-img').forEach(img => {
            if (!img.getAttribute('onerror')) {
                img.dataset.defaultSrc = img.dataset.defaultSrc || config.defaultItemImage || '/images/meal-default.png';
                img.addEventListener('error', function () {
                    posImageFallback(img);
                }, { once: false });
            }
        });

        document.querySelectorAll('.pos-tile').forEach(tile => {
            tile.addEventListener('click', function () {
                App.catalog.tryAddToCart({
                    id: Number(tile.dataset.itemId),
                    itemCode: tile.dataset.itemCode || '',
                    name: tile.dataset.itemName,
                    price: Number(tile.dataset.itemPrice),
                    image: tile.dataset.itemImage || ''
                });
            });
        });
    }

    App.catalog = {
        applySearchFilter,
        bindSchoolSwitchModal,
        confirmSchoolSwitch,

        async tryAddToCart(item) {
            if (!await App.bridge.requireOnline()) return;

            const school = App.helpers.getSelectedSchoolInfo();
            if (!school.id) {
                await App.ui.warning('Select a branch first.');
                return;
            }
            if (!App.helpers.getTerminalCode()) {
                await App.ui.warning('Please select a terminal.');
                return;
            }

            if (state.cart.length > 0 && state.cartSchoolId > 0 && state.cartSchoolId !== school.id) {
                const confirmed = await confirmSchoolSwitch(state.cartSchoolName || 'another school', school.name);
                if (!confirmed) return;
                App.cart.clear();
            }

            state.cartSchoolId = school.id;
            state.cartSchoolName = school.name;
            App.cart.addToCart(item);
        },

        async loadTerminals(schoolId) {
            const form = new FormData();
            form.append('schoolId', schoolId);
            const res = await fetch('/Pos/LoadTerminals', { method: 'POST', body: form });
            document.getElementById('terminalContainer').innerHTML = await res.text();
            bindTerminalDropdown();
            App.header?.updateContext();
        },

        async loadCategories(schoolId) {
            const form = new FormData();
            form.append('schoolId', schoolId);
            const res = await fetch('/Pos/LoadCategories', { method: 'POST', body: form });
            const html = await res.text();
            const nav = document.getElementById('categoryList');
            if (nav) {
                nav.innerHTML = html.trim()
                    ? html
                    : '<p class="pos-categories-placeholder">No categories available.</p>';
            }
            bindCategoryButtons();

            const activeCategory = document.querySelector('.pos-category-item.is-active')
                || document.querySelector('.pos-category-item');
            if (activeCategory) {
                state.selectedCategoryId = Number(activeCategory.dataset.categoryId || 0);
                if (state.selectedCategoryId >= 0) {
                    await App.catalog.loadProducts(schoolId, state.selectedCategoryId);
                } else {
                    document.getElementById('productGrid').innerHTML = '<p class="pos-placeholder">No categories for this school.</p>';
                }
            } else {
                document.getElementById('productGrid').innerHTML = '<p class="pos-placeholder">No categories for this school.</p>';
            }
        },

        async loadProducts(schoolId, categoryId) {
            const form = new FormData();
            form.append('schoolId', schoolId);
            form.append('categoryId', categoryId);
            const res = await fetch('/Pos/LoadProducts', { method: 'POST', body: form });
            document.getElementById('productGrid').innerHTML = await res.text();
            bindProductButtons();
            applySearchFilter();
        },

        async initDefaults() {
            const ddlSchool = document.getElementById('ddlSchool');
            if (!ddlSchool) return;

            let schoolId = Number(ddlSchool.value || 0);
            if (schoolId <= 0) {
                const firstSchool = Array.from(ddlSchool.options).find(o => Number(o.value) > 0);
                if (!firstSchool) return;
                ddlSchool.value = firstSchool.value;
                schoolId = Number(firstSchool.value);
            }

            state.selectedSchoolId = schoolId;
            await Promise.all([
                App.catalog.loadTerminals(schoolId),
                App.catalog.loadCategories(schoolId)
            ]);
            App.header?.updateContext();
            App.header?.closeSetupIfReady();
        }
    };
})(window.PosApp);
