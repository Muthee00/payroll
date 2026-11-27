// Renders <canvas data-chart='{"type":"doughnut",...}'> with Chart.js. Configuration is server-rendered JSON
// (no inline scripts under the CSP). Money values are formatted as KSh in tooltips and axes.
(function () {
    "use strict";
    if (!window.Chart) return;

    var ink = { primary: "#0f172a", secondary: "#475569", muted: "#64748b", grid: "#eef1f6", surface: "#ffffff" };
    var money = new Intl.NumberFormat("en-KE", { maximumFractionDigits: 0 });
    var formatMoney = function (v) { return "KSh " + money.format(v); };

    Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
    Chart.defaults.font.size = 12;
    Chart.defaults.color = ink.secondary;
    Chart.defaults.plugins.tooltip.backgroundColor = "#0b1736";
    Chart.defaults.plugins.tooltip.padding = 10;
    Chart.defaults.plugins.tooltip.cornerRadius = 8;
    Chart.defaults.plugins.tooltip.titleFont = { weight: "600" };

    document.querySelectorAll("canvas[data-chart]").forEach(function (canvas) {
        var spec = JSON.parse(canvas.dataset.chart);
        var isMoney = spec.format === "money";
        var isCircular = spec.type === "doughnut" || spec.type === "pie";

        spec.datasets.forEach(function (ds) {
            if (isCircular) {
                ds.borderColor = ink.surface;   // 2px surface gap between segments
                ds.borderWidth = 2;
                ds.hoverOffset = 6;
            } else if (spec.type === "line") {
                ds.borderWidth = 2;
                ds.pointRadius = 0;
                ds.pointHoverRadius = 5;
                ds.pointHitRadius = 16;
                ds.tension = 0.3;
                ds.backgroundColor = ds.fill ? ds.borderColor + "1f" : ds.borderColor;
            } else if (spec.type === "bar") {
                ds.borderRadius = 4;
                ds.borderSkipped = "start";
                ds.maxBarThickness = 28;
            }
        });

        var options = {
            responsive: true,
            maintainAspectRatio: false,
            interaction: isCircular ? undefined : { mode: "index", intersect: false },
            plugins: {
                legend: { display: spec.legend === true, position: "bottom", labels: { usePointStyle: true, boxWidth: 8, boxHeight: 8, padding: 16 } },
                tooltip: {
                    callbacks: {
                        label: function (ctx) {
                            var value = isCircular ? ctx.parsed : ctx.parsed.y;
                            var text = isMoney ? formatMoney(value) : money.format(value);
                            if (isCircular) {
                                var total = ctx.dataset.data.reduce(function (a, b) { return a + b; }, 0);
                                text += " (" + (total ? (value / total * 100).toFixed(1) : 0) + "%)";
                            }
                            return " " + (ctx.dataset.label && !isCircular ? ctx.dataset.label + ": " : ctx.label + ": ") + text;
                        }
                    }
                }
            }
        };
        if (isCircular) {
            options.cutout = spec.cutout || "70%";
        } else {
            options.scales = {
                x: { grid: { display: false }, border: { display: false }, ticks: { color: ink.muted } },
                y: {
                    beginAtZero: true, border: { display: false }, grid: { color: ink.grid },
                    ticks: {
                        color: ink.muted, maxTicksLimit: 5,
                        callback: function (v) { return isMoney ? "KSh " + (Math.abs(v) >= 1e6 ? (v / 1e6).toFixed(1) + "M" : Math.abs(v) >= 1e3 ? (v / 1e3).toFixed(0) + "K" : v) : v; }
                    }
                }
            };
            if (spec.stacked) { options.scales.x.stacked = true; options.scales.y.stacked = true; }
            if (spec.indexAxis) {
                options.indexAxis = spec.indexAxis;
                var tmp = options.scales.x; options.scales.x = options.scales.y; options.scales.y = tmp;
                options.scales.y.grid = { display: false };
            }
            if (spec.max) options.scales[spec.indexAxis === "y" ? "x" : "y"].max = spec.max;
        }

        new Chart(canvas, { type: spec.type, data: { labels: spec.labels, datasets: spec.datasets }, options: options });
    });
})();
