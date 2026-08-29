(function () {
    'use strict';

    var orderTypeIds = window.ORDER_TYPE_IDS || {};
    var MEAL_ORDER_TYPE = Number(orderTypeIds.MEAL_ORDER) || 0;
    var ALA_CARTE_TYPE = Number(orderTypeIds.ALA_CARTE) || 0;
    var STATUS_HOLIDAY = 0;
    var STATUS_HALFDAY = 2;

    function formatMoney(amount, currency) {
        var value = Number(amount) || 0;
        return currency + ' ' + value.toFixed(2);
    }

    function getToneClass(orderTypeId) {
        var typeId = Number(orderTypeId) || 0;
        if (typeId === MEAL_ORDER_TYPE) {
            return 'tone-combo';
        }

        if (typeId === ALA_CARTE_TYPE) {
            return 'tone-alacarte';
        }

        return '';
    }

    function normalizeDateKey(value) {
        if (!value) {
            return '';
        }

        if (typeof value === 'string' && value.length >= 10) {
            return value.substring(0, 10);
        }

        var date = new Date(value);
        if (isNaN(date.getTime())) {
            return '';
        }

        var month = String(date.getMonth() + 1).padStart(2, '0');
        var day = String(date.getDate()).padStart(2, '0');
        return date.getFullYear() + '-' + month + '-' + day;
    }

    function initOrderCalendar() {
        var page = document.getElementById('orderCalendarPage');
        var calendarEl = document.getElementById('orderCalendar');
        var studentSelect = document.getElementById('calendarStudentId');

        if (!page || !calendarEl || typeof FullCalendar === 'undefined') {
            return;
        }

        var eventsUrl = page.getAttribute('data-events-url') || '';
        var schoolDaysUrl = page.getAttribute('data-school-days-url') || '';
        var detailUrl = page.getAttribute('data-detail-url') || '';
        var currency = page.getAttribute('data-currency') || 'AED';
        var schoolDayMap = {};

        var modalEl = document.getElementById('orderCalendarModal');
        var modalDate = document.getElementById('orderCalendarModalDate');
        var modalChild = document.getElementById('orderCalendarModalChild');
        var modalType = document.getElementById('orderCalendarModalType');
        var modalItems = document.getElementById('orderCalendarModalItems');
        var modalLink = document.getElementById('orderCalendarModalLink');
        var modalInstance = modalEl ? bootstrap.Modal.getOrCreateInstance(modalEl) : null;

        function appendQuery(baseUrl, info) {
            var url = new URL(baseUrl, window.location.origin);
            url.searchParams.set('start', info.startStr);
            url.searchParams.set('end', info.endStr);

            var studentId = studentSelect ? studentSelect.value : '';
            if (studentId) {
                url.searchParams.set('studentId', studentId);
            }

            return url.toString();
        }

        function loadSchoolDays(info) {
            if (!schoolDaysUrl) {
                schoolDayMap = {};
                return Promise.resolve();
            }

            return fetch(appendQuery(schoolDaysUrl, info), {
                headers: { 'Accept': 'application/json' }
            })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error('Failed to load school days.');
                    }

                    return response.json();
                })
                .then(function (days) {
                    schoolDayMap = {};
                    (days || []).forEach(function (day) {
                        var key = normalizeDateKey(day.Date || day.date);
                        if (!key) {
                            return;
                        }

                        schoolDayMap[key] = {
                            status: day.Status != null ? day.Status : day.status,
                            title: day.Title || day.title || '',
                            statusLabel: day.StatusLabel || day.statusLabel || ''
                        };
                    });
                })
                .catch(function () {
                    schoolDayMap = {};
                });
        }

        function annotateSchoolDays() {
            calendarEl.querySelectorAll('.fc-daygrid-day[data-date]').forEach(function (el) {
                el.classList.remove('oc-day-holiday', 'oc-day-halfday');
                var existing = el.querySelector('.oc-day-badge');
                if (existing) {
                    existing.remove();
                }

                var key = el.getAttribute('data-date');
                var day = schoolDayMap[key];
                if (!day) {
                    return;
                }

                var badge = document.createElement('span');
                badge.className = 'oc-day-badge';
                if (day.status === STATUS_HOLIDAY) {
                    el.classList.add('oc-day-holiday');
                    badge.classList.add('holiday');
                    badge.textContent = day.title || day.statusLabel || 'Holiday';
                    el.appendChild(badge);
                } else if (day.status === STATUS_HALFDAY) {
                    el.classList.add('oc-day-halfday');
                    badge.classList.add('halfday');
                    badge.textContent = day.title || day.statusLabel || 'Half day';
                    el.appendChild(badge);
                }
            });
        }

        function normalizeEvent(event) {
            var props = event.extendedProps || event.ExtendedProps || {};
            return {
                mealDate: props.mealDate || props.MealDate || '',
                studentName: props.studentName || props.StudentName || 'Child',
                orderTypeLabel: props.orderTypeLabel || props.OrderTypeLabel || 'Order',
                orderTypeId: props.orderTypeId || props.OrderTypeId || 0,
                items: (props.items || props.Items || []).map(function (item) {
                    return {
                        itemName: item.itemName || item.ItemName || 'Item',
                        itemPrice: item.itemPrice || item.ItemPrice || 0,
                        quantity: item.quantity || item.Quantity || 1,
                        orderId: item.orderId || item.OrderId || ''
                    };
                })
            };
        }

        function openEventModal(event) {
            if (!modalEl || !event) {
                return;
            }

            var props = normalizeEvent(event);
            var items = props.items || [];

            if (modalDate) {
                modalDate.textContent = props.mealDate || '';
            }

            if (modalChild) {
                modalChild.textContent = props.studentName || 'Child';
            }

            if (modalType) {
                modalType.textContent = props.orderTypeLabel || 'Order';
                modalType.className = 'order-calendar-modal-badge ' + getToneClass(props.orderTypeId);
            }

            if (modalItems) {
                modalItems.innerHTML = '';

                items.forEach(function (item) {
                    var li = document.createElement('li');
                    var nameWrap = document.createElement('div');
                    var name = document.createElement('div');
                    var qty = document.createElement('div');
                    var price = document.createElement('div');

                    name.className = 'order-calendar-modal-item-name';
                    name.textContent = item.itemName || 'Item';

                    qty.className = 'order-calendar-modal-item-qty';
                    qty.textContent = (item.quantity || 1) > 1 ? 'Qty ' + item.quantity : '';

                    nameWrap.appendChild(name);
                    if (qty.textContent) {
                        nameWrap.appendChild(qty);
                    }

                    price.className = 'order-calendar-modal-item-price';
                    price.textContent = formatMoney(item.itemPrice, currency);

                    li.appendChild(nameWrap);
                    li.appendChild(price);
                    modalItems.appendChild(li);
                });
            }

            var firstOrderId = items.length > 0 ? items[0].orderId : '';
            if (modalLink) {
                if (firstOrderId) {
                    modalLink.href = detailUrl + '?orderId=' + encodeURIComponent(firstOrderId);
                    modalLink.classList.remove('d-none');
                } else {
                    modalLink.classList.add('d-none');
                }
            }

            if (modalInstance) {
                modalInstance.show();
            }
        }

        var calendar = new FullCalendar.Calendar(calendarEl, {
            initialView: 'dayGridMonth',
            headerToolbar: {
                left: 'prev,next today',
                center: 'title',
                right: ''
            },
            height: 'auto',
            fixedWeekCount: false,
            dayMaxEvents: 3,
            moreLinkClick: 'popover',
            eventDataTransform: function (event) {
                var extendedProps = event.extendedProps || event.ExtendedProps || {};
                return {
                    id: event.id || event.Id,
                    title: event.title || event.Title,
                    start: event.start || event.Start,
                    color: event.color || event.Color,
                    borderColor: event.borderColor || event.BorderColor,
                    textColor: event.textColor || event.TextColor,
                    extendedProps: extendedProps
                };
            },
            events: function (info, successCallback, failureCallback) {
                loadSchoolDays(info)
                    .then(function () {
                        return fetch(appendQuery(eventsUrl, info), {
                            headers: { 'Accept': 'application/json' }
                        });
                    })
                    .then(function (response) {
                        if (!response.ok) {
                            throw new Error('Failed to load calendar events.');
                        }

                        return response.json();
                    })
                    .then(function (events) {
                        successCallback(events);
                        window.requestAnimationFrame(annotateSchoolDays);
                    })
                    .catch(function (error) {
                        failureCallback(error);
                    });
            },
            eventClick: function (info) {
                info.jsEvent.preventDefault();
                openEventModal(info.event);
            },
            eventDidMount: function (info) {
                info.el.setAttribute('title', info.event.title);
            }
        });

        calendar.render();

        if (studentSelect) {
            studentSelect.addEventListener('change', function () {
                calendar.refetchEvents();
            });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initOrderCalendar);
    } else {
        initOrderCalendar();
    }
})();
