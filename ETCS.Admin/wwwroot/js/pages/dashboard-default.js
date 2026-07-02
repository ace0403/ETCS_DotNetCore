'use strict';
document.addEventListener('DOMContentLoaded', function () {
    setTimeout(function () {
        floatchart();
        RevenueChart();
        loadTotalIncomeChart();
    }, 500);
});

$(document).on("click", ".period-link", function () {
    var period = $(this).data("period");

    $.ajax({
        url: '/Dashboard/GetTransactionTotals',
        type: 'GET',
        data: { period: period },
        success: function (data) {
            var container = $("#transaction-type-container");
            container.empty();

            if (!data || data.length === 0) {
                container.append('<div class="col-12 text-center text-muted">No income data found</div>');
            } else {
                data.forEach(item => {
                    container.append(`
                        <div class="col-sm-6">
                            <div class="bg-body p-3 rounded">
                                <div class="d-flex align-items-center mb-2">
                                    <div class="flex-shrink-0">
                                        <span class="p-1 d-block bg-primary rounded-circle"></span>
                                    </div>
                                    <div class="flex-grow-1 ms-2">
                                        <p class="mb-0">${item.name}</p>
                                    </div>
                                </div>
                                <h6 class="mb-0">AED ${item.totalAmount}</h6>
                            </div>
                        </div>
                    `);
                });
            }

            loadTotalIncomeChart(data);
        },
        error: function () {
            alert("Failed to load data. Please try again.");
        }
    });
});

function floatchart() {
    (function () {
        var options1 = {
            chart: { type: 'bar', height: 50, sparkline: { enabled: true } },
            colors: ['#4680FF'],
            plotOptions: { bar: { columnWidth: '80%' } },
            series: [{ data: [10, 30, 40, 20, 60, 50, 20, 15, 20, 25, 30, 25] }],
            xaxis: { crosshairs: { width: 1 } },
            tooltip: { enabled: false }
        };

        ['#all-earnings-graphh', '#all-alacarte-graph', '#totalactive-user-graph', '#all-combo-graph', '#all-earnings-graph'].forEach(id => {
            var el = document.querySelector(id);
            if (el) new ApexCharts(el, options1).render();
        });

        var options2 = {
            chart: { type: 'bar', height: 50, sparkline: { enabled: true } },
            colors: ['#E58A00'],
            plotOptions: { bar: { columnWidth: '80%' } },
            series: [{ data: [10, 30, 40, 20, 60, 50, 20, 15, 20, 25, 30, 25] }],
            xaxis: { crosshairs: { width: 1 } },
            tooltip: { enabled: false }
        };
        var chart = new ApexCharts(document.querySelector('#page-views-graph'), options2);
        chart.render();

        var options3 = {
            chart: { type: 'bar', height: 50, sparkline: { enabled: true } },
            colors: ['#2CA87F'],
            plotOptions: { bar: { columnWidth: '80%' } },
            series: [{ data: [10, 30, 40, 20, 60, 50, 20, 15, 20, 25, 30, 25] }],
            xaxis: { crosshairs: { width: 1 } },
            tooltip: { enabled: false }
        };
        var chart = new ApexCharts(document.querySelector('#total-task-graph'), options3);
        chart.render();

        loadTotalIncomeChart(); 
    })();
}

function RevenueChart() {
    $.ajax({
        url: SiteUrl + 'dashboard/getcharts',
        type: 'GET',
        cache: false,
        success: function (result) {
            if (!result || !result.MonthlyRevenue || result.MonthlyRevenue.MonthList.length === 0) {
                $("#customer-rate-graph").html("<p class='text-center text-muted'>No data available</p>");
                return;
            }

            const months = result.MonthlyRevenue.MonthList;
            const seriesList = result.MonthlyRevenue.SeriesList;
            const colors = ['#4680FF', '#E58A00'];

            const options = {
                chart: {
                    type: 'area',
                    height: 350,
                    toolbar: { show: false },
                    zoom: { enabled: false }
                },
                colors: colors,
                series: seriesList.map(s => ({
                    name: s.name,
                    data: s.data.map(v => Number(v))
                })),
                stroke: {
                    curve: 'smooth',
                    width: 3
                },
                dataLabels: { enabled: false },
                fill: {
                    type: 'gradient',
                    gradient: {
                        shadeIntensity: 1,
                        opacityFrom: 0.4,
                        opacityTo: 0.1,
                        stops: [0, 90, 100]
                    }
                },
                xaxis: {
                    categories: months,
                    title: { text: 'Month' }
                },
                yaxis: {
                    title: { text: 'Revenue (AED)' }
                },
                tooltip: {
                    y: { formatter: val => "AED " + val.toFixed(2) }
                },
                legend: {
                    position: 'bottom',
                    horizontalAlign: 'center'
                }
            };

            if (window.monthlyRevenueChart) window.monthlyRevenueChart.destroy();

            window.monthlyRevenueChart = new ApexCharts(
                document.querySelector("#customer-rate-graph"),
                options
            );
            window.monthlyRevenueChart.render();
        },
        error: function () {
            $("#customer-rate-graph").html("<p class='text-danger text-center'>Error loading chart.</p>");
        }
    });
}

function loadTotalIncomeChart(data) {
    var transactionData = data || [];

    if (transactionData.length === 0) {
        transactionData = [];
        $("#transaction-type-container .col-sm-6").each(function () {
            var name = $(this).find(".flex-grow-1 p").text().trim();
            var amount = parseFloat($(this).find("h6").text().replace('AED', '').trim());
            transactionData.push({ name: name, totalAmount: amount });
        });
    }

    var series = transactionData.map(x => x.totalAmount);
    var labels = transactionData.map(x => x.name);

    var colors = ['#4680FF', '#E58A00', '#2CA87F', '#FFC107', '#1ABC9C'];

    var options8 = {
        chart: { height: 320, type: 'donut' },
        series: series,
        colors: colors.slice(0, labels.length),
        labels: labels,
        fill: { opacity: 1 },
        legend: { show: false },
        plotOptions: { pie: { donut: { size: '65%', labels: { show: true, name: { show: true }, value: { show: true } } } } },
        dataLabels: { enabled: false },
        responsive: [{ breakpoint: 480, options: { plotOptions: { pie: { donut: { size: '65%', labels: { show: true } } } } } }]
    };

    // Destroy previous chart if exists
    if (window.totalIncomeChart) {
        window.totalIncomeChart.destroy();
    }
    window.totalIncomeChart = new ApexCharts(document.querySelector('#total-income-graph'), options8);
    window.totalIncomeChart.render();
}

