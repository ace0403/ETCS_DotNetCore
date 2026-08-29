var previewSample = {
    '{{GuardianName}}': 'Jane Guardian',
    '{{StudentName}}': 'Alex Student',
    '{{OrderId}}': 'ORD-2026-001',
    '{{TransactionId}}': 'TXN-998877',
    '{{Amount}}': '125.00',
    '{{EventDate}}': '07-06-2026 02:30:00 PM',
    '{{OrderItems}}': '- 07-06-2026 | Chicken Wrap | 25.00<br/>- 08-06-2026 | Fruit Bowl | 15.00',
    '{{CardNumber}}': '1234567890',
    '{{CustomerId}}': 'CUST-1001',
    '{{Reason}}': 'Card lost',
    '{{RefCode}}': '42',
    '{{SchoolName}}': 'Emirates International School',
    '{{LogoUrl}}': '/images/logo.png',
    '{{OtpCode}}': '482913',
    '{{ResetLink}}': 'https://parent.etcs.example/reset-password',
    '{{ExpiryMinutes}}': '10',
    '{{AddChildLink}}': 'https://parent.etcs.example/MyKids'
};

function applyPreviewPlaceholders(text) {
    if (!text) return '';
    var result = text;
    Object.keys(previewSample).forEach(function (key) {
        result = result.split(key).join(previewSample[key]);
    });
    return result;
}

function updateEmailPreview() {
    var subject = applyPreviewPlaceholders($('#emailSubject').val() || '');
    var body = applyPreviewPlaceholders($('#emailBodyEditor').val() || '');
    var doc = $('#emailPreviewFrame')[0].contentDocument || $('#emailPreviewFrame')[0].contentWindow.document;
    doc.open();
    doc.write('<!DOCTYPE html><html><head><title>' + subject + '</title></head><body>' + body + '</body></html>');
    doc.close();
}

var myTable = initAdminDataTable('#grid_table', 'emailtemplate/getlist', [
    { data: 'TemplateKey' },
    { data: 'SubjectTemplate' },
    {
        data: 'IsActive',
        render: function (d) { return d ? '<span class="badge bg-success">Yes</span>' : '<span class="badge bg-secondary">No</span>'; }
    },
    {
        data: 'UpdatedOn',
        render: function (d, type, row) {
            var value = d || row.CreatedOn;
            if (!value) return '';
            var date = new Date(value);
            return isNaN(date.getTime()) ? value : date.toLocaleString();
        }
    },
    {
        data: 'TemplateKey',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) {
            return '<button type="button" class="btn btn-sm btn-outline-primary" onclick="loadData(\'' + d + '\')" title="Edit"><i class="ti ti-edit"></i></button>';
        }
    }
], { order: [[0, 'asc']], paging: false, searching: false });

function loadData(templateKey) {
    $.get(SiteUrl + 'emailtemplate/get?templateKey=' + encodeURIComponent(templateKey), function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        bindEmailTemplateForm();
    });
}

function bindEmailTemplateForm() {
    updateEmailPreview();

    $('#emailSubject').on('input', updateEmailPreview);
    $('#emailBodyEditor').on('input', updateEmailPreview);

    $('.email-placeholder-chip').on('click', function () {
        var placeholder = $(this).data('placeholder');
        var $body = $('#emailBodyEditor');
        var el = $body[0];
        var start = el.selectionStart;
        var end = el.selectionEnd;
        var text = $body.val();
        $body.val(text.substring(0, start) + placeholder + text.substring(end));
        el.focus();
        el.selectionStart = el.selectionEnd = start + placeholder.length;
        updateEmailPreview();
    });

    bindAdminFormSave('#frmEmailTemplate', function ($form) {
        $.post(SiteUrl + 'emailtemplate/save', $form.serialize(), function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) {
                myTable.ajax.reload();
                $('#addDataModal').modal('hide');
            }
        });
    });
}
