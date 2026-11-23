(function () {
    const config = window.posReceiptReprintConfig || {};
    const bridgeBaseUrl = String(config.bridgeBaseUrl || 'http://127.0.0.1:5050').replace(/\/$/, '');
    let reportTable = null;
    let currentReceipt = null;
    let bridgeOnline = false;

    const els = {
        notice: document.getElementById('receipt-notice'),
        previewHost: document.getElementById('receipt-preview-host'),
        printRoot: document.getElementById('receipt-print-root'),
        bridgeStatus: document.getElementById('bridge-status'),
        btnPreview: document.getElementById('btnPreviewReceipt'),
        btnBrowser: document.getElementById('btnBrowserPrint'),
        btnThermal: document.getElementById('btnThermalPrint'),
        emptyMessage: document.getElementById('reportEmptyMessage')
    };

    function escapeHtml(value) {
        return String(value || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function formatDateTime(value) {
        if (!value) return '';
        const d = new Date(value);
        if (Number.isNaN(d.getTime())) return String(value);
        const pad = (n) => String(n).padStart(2, '0');
        return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }

    function formatGridDateTime(value) {
        if (!value) return '';
        const d = new Date(value);
        if (Number.isNaN(d.getTime())) return String(value);
        const pad = (n) => String(n).padStart(2, '0');
        return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }

    function money(value) {
        const n = Number(value || 0);
        return n.toFixed(2);
    }

    function getSelectedSchoolId() {
        return parseInt($('#ddlSchool').val() || '0', 10) || 0;
    }

    function getFilters() {
        return {
            From: $('#txtFrom').val() || '',
            To: $('#txtTo').val() || '',
            SchoolId: getSelectedSchoolId(),
            TerminalCode: ($('#txtTerminalCode').val() || '').trim(),
            TransactionId: ($('#txtTransactionId').val() || '').trim(),
            CustomerId: ($('#txtCustomerId').val() || '').trim()
        };
    }

    function validateFilters(filters) {
        if (!filters.From || !filters.To) {
            if (typeof toastMsg === 'function') toastMsg('From and To dates are required.', false);
            else window.alert('From and To dates are required.');
            return false;
        }
        if (filters.From > filters.To) {
            if (typeof toastMsg === 'function') toastMsg('From date should be less than or equal to To date.', false);
            else window.alert('From date should be less than or equal to To date.');
            return false;
        }
        const fromDate = new Date(filters.From);
        const toDate = new Date(filters.To);
        const daySpan = Math.round((toDate - fromDate) / 86400000);
        if (daySpan > 31) {
            if (typeof toastMsg === 'function') toastMsg('Date range is limited to 31 days.', false);
            else window.alert('Date range is limited to 31 days.');
            return false;
        }
        if (!filters.SchoolId) {
            if (typeof toastMsg === 'function') toastMsg('School is required.', false);
            else window.alert('School is required.');
            return false;
        }
        return true;
    }

    function setReportLoading(isLoading, message) {
        const $loader = $('#posReprintLoader');
        const $wrap = $('#posReprintGridWrap');
        const $viewBtn = $('#btnViewReport');
        if (message) {
            $loader.find('.canteen-report-loader-text').text(message);
        }
        $loader.toggleClass('d-none', !isLoading);
        $wrap.toggleClass('is-loading', isLoading);
        $('body').toggleClass('canteen-report-loading', isLoading);
        $viewBtn.prop('disabled', isLoading);
        $viewBtn.find('.btn-spinner').toggleClass('d-none', !isLoading);
    }

    function setNotice(message) {
        if (!els.notice) return;
        if (!message) {
            els.notice.classList.add('d-none');
            els.notice.textContent = '';
            return;
        }
        els.notice.textContent = message;
        els.notice.classList.remove('d-none');
    }

    function setActionsEnabled(enabled) {
        [els.btnPreview, els.btnBrowser].forEach((btn) => {
            if (btn) btn.disabled = !enabled;
        });
        if (els.btnThermal) {
            els.btnThermal.disabled = !(enabled && bridgeOnline);
        }
    }

    function buildReceiptHtml(receipt) {
        const items = Array.isArray(receipt.items) ? receipt.items : [];
        const itemRows = items.map((item) => {
            const qty = Number(item.quantity || 1);
            const label = qty > 1 ? `${escapeHtml(item.name)} x${qty}` : escapeHtml(item.name);
            return `<div class="pos-receipt-line"><span>${label}</span><span>${money(item.price)}</span></div>`;
        }).join('');

        const logo = receipt.logoBase64
            ? `<img class="pos-receipt-logo" src="${escapeHtml(receipt.logoBase64)}" alt="" />`
            : '';

        const saleBlock = receipt.isReprint
            ? `<div>Original Sale: ${escapeHtml(formatDateTime(receipt.printedAt))}</div>
               <div>Reprinted at: ${escapeHtml(formatDateTime(receipt.reprintedAt || new Date().toISOString()))}</div>`
            : `<div>${escapeHtml(formatDateTime(receipt.printedAt || new Date().toISOString()))}</div>`;

        const title = receipt.isUndo ? 'UNDO RECEIPT' : 'TAX INVOICE';
        const txn = receipt.transactionId
            ? `<div>Transaction ID: ${escapeHtml(receipt.transactionId)}</div>`
            : '';
        const reprintFooter = receipt.isReprint ? '<div class="pos-receipt-reprint">REPRINT</div>' : '';

        return `
<div class="pos-receipt-slip">
  ${logo}
  <div class="pos-receipt-center pos-receipt-bold">${escapeHtml(receipt.companyLine || '')}</div>
  <div class="pos-receipt-center pos-receipt-bold">${escapeHtml(receipt.vatRegNoLine || '')}</div>
  <div class="pos-receipt-center">${escapeHtml(receipt.locationLine || '')}</div>
  <div>Terminal: ${escapeHtml(receipt.terminalLine || '')}</div>
  <div class="pos-receipt-bold">Payment Mode: ${escapeHtml(receipt.paymentMode || '')}</div>
  <div class="pos-receipt-center pos-receipt-title">${title}</div>
  ${saleBlock}
  ${txn}
  <hr class="pos-receipt-rule" />
  ${itemRows || '<div class="text-muted">No items</div>'}
  <hr class="pos-receipt-rule" />
  <div class="pos-receipt-line pos-receipt-bold"><span>Total AED</span><span>${money(receipt.total)}</span></div>
  <div class="pos-receipt-center">Thank You!</div>
  <div class="pos-receipt-center">Visit Again.</div>
  ${reprintFooter}
</div>`;
    }

    function renderReceipt(receipt) {
        currentReceipt = receipt;
        const html = buildReceiptHtml(receipt);
        if (els.previewHost) els.previewHost.innerHTML = html;
        if (els.printRoot) els.printRoot.innerHTML = html;
        setActionsEnabled(true);
    }

    async function loadDetail(transactionId, customerId) {
        setActionsEnabled(false);
        setNotice('');
        if (els.previewHost) {
            els.previewHost.innerHTML = '<div class="text-muted">Loading receipt…</div>';
        }

        const url = `${config.detailUrl}?transactionId=${encodeURIComponent(transactionId)}&customerId=${encodeURIComponent(customerId)}`;
        try {
            const res = await fetch(url, { headers: { Accept: 'application/json' } });
            const data = await res.json().catch(() => ({}));
            if (!res.ok || !data.success || !data.receipt) {
                if (els.previewHost) {
                    els.previewHost.innerHTML = '<div class="text-danger">Unable to load receipt.</div>';
                }
                setNotice(data.message || 'Unable to load receipt.');
                return;
            }
            setNotice(data.notice || '');
            renderReceipt(data.receipt);
        } catch {
            if (els.previewHost) {
                els.previewHost.innerHTML = '<div class="text-danger">Unable to load receipt.</div>';
            }
            setNotice('Unable to load receipt.');
        }
    }

    window.selectPosReprintSale = function (transactionId, customerId) {
        loadDetail(transactionId, customerId);
    };

    async function checkBridge() {
        if (!els.bridgeStatus) return;
        try {
            const res = await fetch(bridgeBaseUrl + '/health', { method: 'GET' });
            bridgeOnline = res.ok;
            els.bridgeStatus.textContent = bridgeOnline
                ? 'Bridge connected — thermal print available.'
                : 'Bridge offline — thermal print disabled.';
            els.bridgeStatus.className = bridgeOnline ? 'small text-success mb-2' : 'small text-warning mb-2';
        } catch {
            bridgeOnline = false;
            els.bridgeStatus.textContent = 'Bridge offline — thermal print disabled.';
            els.bridgeStatus.className = 'small text-warning mb-2';
        }
        if (currentReceipt) setActionsEnabled(true);
    }

    function browserPrint() {
        if (!currentReceipt || !els.printRoot) return;
        els.printRoot.classList.remove('d-none');
        document.body.classList.add('pos-receipt-printing');
        window.print();
        setTimeout(() => {
            document.body.classList.remove('pos-receipt-printing');
            els.printRoot.classList.add('d-none');
        }, 300);
    }

    async function thermalPrint() {
        if (!currentReceipt || !bridgeOnline) {
            window.alert('Local Bridge is not available on this machine.');
            return;
        }
        try {
            const res = await fetch(bridgeBaseUrl + '/print/receipt', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(currentReceipt)
            });
            const data = await res.json().catch(() => ({}));
            if (!res.ok || data.isSuccess === false) {
                window.alert(data.message || 'Thermal print failed.');
                return;
            }
            window.alert(data.message || 'Receipt sent to printer.');
        } catch {
            window.alert('Unable to reach Bridge at ' + bridgeBaseUrl);
        }
    }

    function initReportTable() {
        if (reportTable) {
            reportTable.ajax.reload(null, true);
            return;
        }

        const ajaxConfig = adminDataTableAjax(config.listUrl || 'posreceiptreprint/getlist');
        ajaxConfig.data = function (payload) {
            const filters = getFilters();
            payload.From = filters.From;
            payload.To = filters.To;
            payload.SchoolId = filters.SchoolId;
            payload.TerminalCode = filters.TerminalCode;
            payload.TransactionId = filters.TransactionId;
            payload.CustomerId = filters.CustomerId;
        };
        ajaxConfig.dataFilter = function (raw) {
            const j = JSON.parse(raw);
            if (j.Success === false && j.Message) {
                if (typeof toastMsg === 'function') toastMsg(j.Message, false);
            }
            if ((j.RecordsFiltered || 0) === 0) {
                $(els.emptyMessage).text('No POS sales found for the selected filters.').show();
            } else {
                $(els.emptyMessage).hide();
            }
            return JSON.stringify({
                draw: j.Draw,
                recordsTotal: j.RecordsTotal,
                recordsFiltered: j.RecordsFiltered,
                data: j.Data || []
            });
        };

        reportTable = $('#grid_table').DataTable({
            processing: false,
            serverSide: true,
            searching: false,
            ordering: false,
            responsive: false,
            scrollX: true,
            pageLength: 25,
            lengthMenu: [10, 25, 50, 100],
            autoWidth: true,
            deferLoading: 0,
            language: {
                emptyTable: 'No POS sales found.',
                info: 'Showing _START_ to _END_ of _TOTAL_ sales',
                infoEmpty: 'Showing 0 to 0 of 0 sales',
                paginate: {
                    previous: 'Prev',
                    next: 'Next'
                }
            },
            layout: {
                topStart: 'pageLength',
                topEnd: null,
                bottomStart: 'info',
                bottomEnd: 'paging'
            },
            ajax: ajaxConfig,
            columns: [
                {
                    data: null,
                    orderable: false,
                    searchable: false,
                    className: 'text-center text-nowrap',
                    render: function (_d, _t, row) {
                        const txn = String(row.TransactionId || '').replace(/'/g, "\\'");
                        const cust = String(row.CustomerId || '').replace(/'/g, "\\'");
                        return '<button type="button" class="btn btn-sm btn-outline-primary" onclick="selectPosReprintSale(\'' +
                            txn + '\',\'' + cust + '\'); return false;">Select</button>';
                    }
                },
                {
                    data: 'SaleDateTime',
                    render: function (d) { return escapeHtml(formatGridDateTime(d)); }
                },
                // {
                //     data: 'TransactionId',
                //     render: function (d) { return '<code>' + escapeHtml(d) + '</code>'; }
                // },
                {
                    data: null,
                    render: function (_d, _t, row) {
                        const name = row.StudentName ? escapeHtml(row.StudentName) : '—';
                        return name + '<br /><small class="text-muted">' + escapeHtml(row.CustomerId || '') + '</small>';
                    }
                },
                { data: 'PaymentMode' },
                {
                    data: 'Amount',
                    className: 'text-end',
                    render: function (d) { return money(d); }
                },
                {
                    data: null,
                    render: function (_d, _t, row) {
                        return escapeHtml(row.TerminalName || row.TerminalCode || '');
                    }
                },
                {
                    data: 'HasLineItems',
                    className: 'text-center',
                    render: function (d) {
                        return d
                            ? '<span class="badge text-bg-success">Yes</span>'
                            : '<span class="badge text-bg-secondary">No</span>';
                    }
                }
            ]
        });

        reportTable.on('preXhr.dt', function () {
            setReportLoading(true, 'Loading sales...');
        });
        reportTable.on('draw.dt', function () {
            setReportLoading(false);
            reportTable.columns.adjust();
        });
        reportTable.on('error.dt', function () {
            setReportLoading(false);
        });

        reportTable.draw();
    }

    function viewReport() {
        const filters = getFilters();
        if (!validateFilters(filters)) return;
        setReportLoading(true, 'Loading sales...');
        initReportTable();
    }

    $(function () {
        $('#btnViewReport').on('click', viewReport);
        if (els.btnPreview) {
            els.btnPreview.addEventListener('click', function () {
                if (!currentReceipt) return;
                els.previewHost?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            });
        }
        if (els.btnBrowser) els.btnBrowser.addEventListener('click', browserPrint);
        if (els.btnThermal) els.btnThermal.addEventListener('click', thermalPrint);
        checkBridge();
    });
})();
