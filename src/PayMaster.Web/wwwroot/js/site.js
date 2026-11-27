// PayMaster UI behaviours. No inline scripts are used (strict CSP), so pages opt in via data attributes.
(function () {
    "use strict";

    // Mobile sidebar
    document.addEventListener("click", function (e) {
        if (e.target.closest("[data-sidebar-toggle]")) {
            document.body.classList.toggle("sidebar-open");
        } else if (e.target.closest(".sidebar-backdrop")) {
            document.body.classList.remove("sidebar-open");
        }
    });

    // Server-rendered toasts
    document.querySelectorAll(".toast-pm").forEach(function (el) {
        bootstrap.Toast.getOrCreateInstance(el, { delay: el.classList.contains("toast-error") ? 8000 : 4500 }).show();
    });

    window.pmToast = function (message, type) {
        var stack = document.querySelector(".toast-stack");
        if (!stack) return;
        var el = document.createElement("div");
        el.className = "toast toast-pm toast-" + (type || "success");
        el.setAttribute("role", "status");
        el.innerHTML = '<div class="toast-body d-flex gap-2 align-items-start"><span class="flex-grow-1"></span>' +
            '<button type="button" class="btn-close btn-sm" data-bs-dismiss="toast" aria-label="Close"></button></div>';
        el.querySelector("span").textContent = message;
        stack.appendChild(el);
        bootstrap.Toast.getOrCreateInstance(el).show();
    };

    // Confirmation dialog: <form data-confirm="Approve payroll?" data-confirm-title="..." data-confirm-variant="danger">
    var confirmModalEl = document.getElementById("confirmModal");
    var pendingForm = null;
    if (confirmModalEl) {
        var confirmModal = bootstrap.Modal.getOrCreateInstance(confirmModalEl);
        document.addEventListener("submit", function (e) {
            var form = e.target;
            if (!form.matches("form[data-confirm]") || form.dataset.confirmed === "true") return;
            e.preventDefault();
            pendingForm = form;
            confirmModalEl.querySelector("[data-confirm-title]").textContent = form.dataset.confirmTitle || "Please confirm";
            confirmModalEl.querySelector("[data-confirm-message]").textContent = form.dataset.confirm;
            var okButton = confirmModalEl.querySelector("[data-confirm-ok]");
            okButton.className = "btn btn-" + (form.dataset.confirmVariant || "primary");
            okButton.textContent = form.dataset.confirmButton || "Confirm";
            var reasonGroup = confirmModalEl.querySelector("[data-confirm-reason-group]");
            var reasonInput = confirmModalEl.querySelector("[data-confirm-reason]");
            var needsReason = form.dataset.confirmReason === "true";
            reasonGroup.classList.toggle("d-none", !needsReason);
            reasonInput.value = "";
            reasonInput.required = needsReason;
            confirmModal.show();
        }, true);

        confirmModalEl.querySelector("[data-confirm-ok]").addEventListener("click", function () {
            if (!pendingForm) return;
            var reasonInput = confirmModalEl.querySelector("[data-confirm-reason]");
            if (reasonInput.required) {
                if (!reasonInput.value.trim()) { reasonInput.classList.add("is-invalid"); return; }
                var hidden = pendingForm.querySelector("input[name='reason']") || document.createElement("input");
                hidden.type = "hidden"; hidden.name = "reason"; hidden.value = reasonInput.value.trim();
                pendingForm.appendChild(hidden);
            }
            reasonInput.classList.remove("is-invalid");
            pendingForm.dataset.confirmed = "true";
            confirmModal.hide();
            if (pendingForm.requestSubmit) pendingForm.requestSubmit(); else pendingForm.submit();
        });
    }

    // Prevent double submission of state-changing forms and show progress.
    document.addEventListener("submit", function (e) {
        var form = e.target;
        if (e.defaultPrevented || form.method.toLowerCase() !== "post" || form.hasAttribute("data-no-busy")) return;
        if (form.dataset.submitting === "true") { e.preventDefault(); return; }
        form.dataset.submitting = "true";
        var button = e.submitter || form.querySelector("[type=submit]");
        if (button) {
            button.disabled = true;
            button.dataset.originalHtml = button.innerHTML;
            button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>' + button.textContent.trim();
            if (button.name) {
                var mirror = document.createElement("input");
                mirror.type = "hidden"; mirror.name = button.name; mirror.value = button.value;
                form.appendChild(mirror);
            }
        }
    });
    window.addEventListener("pageshow", function () {
        document.querySelectorAll("form[data-submitting]").forEach(function (form) {
            delete form.dataset.submitting;
            delete form.dataset.confirmed;
            form.querySelectorAll("[data-original-html]").forEach(function (b) { b.disabled = false; b.innerHTML = b.dataset.originalHtml; });
        });
    });

    // Auto-submit filter controls: <select data-autosubmit>
    document.addEventListener("change", function (e) {
        if (e.target.matches("[data-autosubmit]")) e.target.form.submit();
    });

    // Clickable table rows: <tr data-href="/x">
    document.addEventListener("click", function (e) {
        var row = e.target.closest("tr[data-href]");
        if (row && !e.target.closest("a, button, input, form, .dropdown")) window.location = row.dataset.href;
    });

    // Dependent dropdowns: <select data-filter-parent="#dept"> with <option data-parent="deptId">
    document.querySelectorAll("select[data-filter-parent]").forEach(function (child) {
        var parent = document.querySelector(child.dataset.filterParent);
        if (!parent) return;
        var apply = function () {
            var parentValue = parent.value;
            Array.from(child.options).forEach(function (opt) {
                if (!opt.dataset.parent) return;
                var visible = !parentValue || parentValue === "0" || opt.dataset.parent === parentValue;
                opt.hidden = !visible;
                opt.disabled = !visible;
                if (!visible && opt.selected) child.value = "";
            });
        };
        parent.addEventListener("change", apply);
        apply();
    });

    // Prefill a field from the selected option: <select data-prefill-target="#x"><option data-prefill="123">
    document.querySelectorAll("select[data-prefill-target]").forEach(function (select) {
        select.addEventListener("change", function () {
            var target = document.querySelector(select.dataset.prefillTarget);
            var option = select.options[select.selectedIndex];
            if (target && option && option.dataset.prefill && !target.value) target.value = option.dataset.prefill;
        });
    });

    // Open the tab named in the URL hash (#documents) and keep the hash in sync.
    var tabTrigger = location.hash && document.querySelector("[data-bs-toggle='tab'][href='" + location.hash + "']");
    if (tabTrigger) bootstrap.Tab.getOrCreateInstance(tabTrigger).show();
    document.querySelectorAll("[data-bs-toggle='tab']").forEach(function (el) {
        el.addEventListener("shown.bs.tab", function () { history.replaceState(null, "", el.getAttribute("href")); });
    });

    // Period picker writing to hidden Year/Month inputs: <select data-period-target> with values "YYYY-MM".
    document.querySelectorAll("select[data-period-target]").forEach(function (select) {
        var sync = function () {
            var parts = select.value.split("-");
            var y = document.getElementById("inputYear"), m = document.getElementById("inputMonth");
            if (y && m && parts.length === 2) { y.value = parseInt(parts[0], 10); m.value = parseInt(parts[1], 10); }
        };
        select.addEventListener("change", sync);
        sync();
    });

    // Payroll breakdown drawer: clicking [data-line-url] loads the fragment into #lineDrawer.
    var drawerEl = document.getElementById("lineDrawer");
    if (drawerEl) {
        var drawer = bootstrap.Offcanvas.getOrCreateInstance(drawerEl);
        var body = drawerEl.querySelector(".offcanvas-body");
        document.addEventListener("click", function (e) {
            var trigger = e.target.closest("[data-line-url]");
            if (!trigger || e.target.closest("form, .dropdown, a[href]:not([data-line-url])")) return;
            e.preventDefault();
            body.innerHTML = '<div class="d-grid gap-2"><span class="skeleton" style="height:22px;width:60%"></span><span class="skeleton" style="height:120px"></span><span class="skeleton" style="height:160px"></span></div>';
            drawer.show();
            fetch(trigger.dataset.lineUrl, { headers: { "X-Requested-With": "fetch" }, credentials: "same-origin" })
                .then(function (r) { if (!r.ok) throw new Error(r.status); return r.text(); })
                .then(function (html) { body.innerHTML = html; })
                .catch(function () { body.innerHTML = '<div class="alert alert-danger">The breakdown could not be loaded. Please try again.</div>'; });
        });
    }

    // Auto-refresh while a background job runs: <div data-auto-refresh="5">
    var refresh = document.querySelector("[data-auto-refresh]");
    if (refresh) setTimeout(function () { location.reload(); }, (parseInt(refresh.dataset.autoRefresh, 10) || 5) * 1000);

    // Show/hide blocks by a radio group: <input name="Scope" data-toggle-group> + <div data-show-when="Scope=Department">
    document.querySelectorAll("[data-show-when]").forEach(function (block) {
        var parts = block.dataset.showWhen.split("=");
        var radios = document.querySelectorAll("input[name='" + parts[0] + "']");
        var apply = function () {
            var checked = document.querySelector("input[name='" + parts[0] + "']:checked");
            block.classList.toggle("d-none", !checked || checked.value !== parts[1]);
        };
        radios.forEach(function (r) { r.addEventListener("change", apply); });
        apply();
    });

    document.addEventListener("click", function (e) {
        if (e.target.closest("[data-print]")) window.print();
    });

    // Bootstrap tooltips
    document.querySelectorAll("[data-bs-toggle='tooltip']").forEach(function (el) { new bootstrap.Tooltip(el); });
})();
