import { clientProfiles } from "./client-profiles.js";
import { createClientPreview } from "./client-preview.js";

const root = "__EMAIL_KITCHEN_ROOT__";
const byId = id => document.getElementById(id);
let frame = byId("preview");
let pendingFrame = null;
const status = byId("action-status");
const originalMediaQueries = new WeakMap();
const clientPreview = createClientPreview({
    surface: frame.parentElement,
    onRender: setPreviewHtml,
    cancelPendingRender: cancelPendingPreview,
    readPreference,
    savePreference
});

function readPreference(key) {
    try {
        return localStorage.getItem(key);
    } catch {
        return null;
    }
}

function savePreference(key, value) {
    try {
        localStorage.setItem(key, value);
    } catch {
        // The kitchen still works when browser storage is unavailable.
    }
}

function initializeHeaderOptions() {
    const options = byId("header-options");
    const compact = matchMedia("(max-width: 760px)");

    function apply() {
        options.open = !compact.matches;
    }

    apply();
    compact.addEventListener("change", apply);
}

function initializeTheme() {
    const key = "emailkitchen.theme";
    const select = byId("theme");
    const system = matchMedia("(prefers-color-scheme: dark)");

    function apply(preference) {
        select.value = preference === "light" || preference === "dark" ? preference : "system";
        document.documentElement.dataset.theme = select.value === "system"
            ? (system.matches ? "dark" : "light")
            : select.value;
        applyPreviewTheme();
        clientPreview.setTheme(document.documentElement.dataset.theme);
    }

    apply(readPreference(key));
    select.addEventListener("change", () => {
        apply(select.value);
        savePreference(key, select.value);
    });
    system.addEventListener("change", () => apply(select.value));
    window.addEventListener("storage", event => {
        if (event.key === key || event.key === null)
            apply(event.newValue);
    });
}

function initializePreviewWidth() {
    const key = "emailkitchen.preview-width";
    const mobileWidthKey = "emailkitchen.mobile-preview-width";
    const compact = matchMedia("(max-width: 760px), (hover: none) and (pointer: coarse)");
    const minWidth = 320;
    const maxWidth = 760;
    const defaultWidth = 390;
    const buttons = document.querySelectorAll(".view-controls [data-width]");
    const handle = byId("preview-resize-handle");
    let preference = readPreference(key);
    let mobileWidth = normalizeMobileWidth(readPreference(mobileWidthKey));
    let activePointer = null;
    let dragStartX = 0;
    let dragStartWidth = mobileWidth;

    function normalizeMobileWidth(value) {
        const parsed = Number(value);
        return value === null || !Number.isFinite(parsed)
            ? defaultWidth
            : Math.max(minWidth, Math.min(maxWidth, Math.round(parsed)));
    }

    function applyMobileWidth() {
        document.documentElement.style.setProperty("--emailkitchen-mobile-preview-width", `${mobileWidth}px`);
        handle.setAttribute("aria-valuenow", String(mobileWidth));
        handle.setAttribute("aria-valuetext", `${mobileWidth} pixels`);
    }

    function setMobileWidth(value, persist = false) {
        mobileWidth = normalizeMobileWidth(value);
        applyMobileWidth();
        if (persist)
            savePreference(mobileWidthKey, String(mobileWidth));
    }

    function apply() {
        const width = !compact.matches && preference === "mobile" ? "mobile" : "desktop";
        document.documentElement.dataset.previewWidth = width;
        handle.hidden = width !== "mobile";
        for (const button of buttons)
            button.setAttribute("aria-pressed", String(button.dataset.width === width));
    }

    applyMobileWidth();
    apply();
    compact.addEventListener("change", apply);
    for (const button of buttons) {
        button.addEventListener("click", () => {
            preference = button.dataset.width;
            apply();
            savePreference(key, preference);
        });
    }
    handle.addEventListener("pointerdown", event => {
        if (event.button !== 0 || document.documentElement.dataset.previewWidth !== "mobile")
            return;

        event.preventDefault();
        activePointer = event.pointerId;
        dragStartX = event.clientX;
        dragStartWidth = mobileWidth;
        handle.setPointerCapture(event.pointerId);
    });
    handle.addEventListener("pointermove", event => {
        if (event.pointerId === activePointer)
            setMobileWidth(dragStartWidth - event.clientX + dragStartX);
    });

    function finishResize(event) {
        if (event.pointerId !== activePointer)
            return;

        activePointer = null;
        savePreference(mobileWidthKey, String(mobileWidth));
    }

    handle.addEventListener("pointerup", finishResize);
    handle.addEventListener("pointercancel", finishResize);
    handle.addEventListener("keydown", event => {
        const steps = { ArrowLeft: -10, ArrowRight: 10, PageDown: -40, PageUp: 40 };
        if (event.key === "Home") {
            event.preventDefault();
            setMobileWidth(minWidth, true);
        } else if (event.key === "End") {
            event.preventDefault();
            setMobileWidth(maxWidth, true);
        } else if (steps[event.key]) {
            event.preventDefault();
            setMobileWidth(mobileWidth + steps[event.key], true);
        }
    });
    window.addEventListener("storage", event => {
        if (event.key === key || event.key === null) {
            preference = event.newValue;
            apply();
        }
        if (event.key === mobileWidthKey || event.key === null)
            setMobileWidth(event.newValue);
    });
}

function applyPreviewTheme(previewFrame = frame) {
    const preview = previewFrame.contentDocument;
    if (!preview)
        return;

    const theme = document.documentElement.dataset.theme;
    preview.documentElement.style.colorScheme = theme;
    const client = clientProfiles.find(profile => profile.id === previewFrame.dataset.client);
    const mediaTheme = client?.ignoresColorScheme ? null : theme;
    // Evaluate theme queries for the preview even when the browser reports its system theme inside the iframe.
    for (const sheet of preview.styleSheets) {
        try {
            applyThemeQueries(sheet.cssRules, mediaTheme);
        } catch (error) {
            if (error.name !== "SecurityError")
                throw error;
        }
    }
}

function applyThemeQueries(rules, theme) {
    for (const rule of rules) {
        if (rule.type === CSSRule.MEDIA_RULE) {
            const original = originalMediaQueries.get(rule) ?? rule.media.mediaText;
            const updated = original.replace(/\(\s*prefers-color-scheme\s*:\s*(light|dark)\s*\)/gi,
                (_, scheme) => scheme.toLowerCase() === theme ? "(min-width: 0px)" : "(width < 0px)");
            const media = theme === null && original !== updated ? "not all" : updated;
            if (rule.media.mediaText !== media) {
                originalMediaQueries.set(rule, original);
                rule.media.mediaText = media;
            }
        }
        if (rule.cssRules)
            applyThemeQueries(rule.cssRules, theme);
    }
}

function templatePath(name) {
    return name.split("/").map(encodeURIComponent).join("/");
}

function templateLabel(name) {
    return name.split("/").at(-1).replace(/([a-z0-9])([A-Z])/g, "$1 $2");
}

function previewState(state, message) {
    byId("preview-state").dataset.state = state;
    byId("preview-status").textContent = message;
}

function showMessage(element, message, error = false) {
    element.textContent = message;
    element.dataset.error = String(error);
}

async function request(path, options) {
    const response = await fetch(root + path, { cache: "no-store", ...options });
    if (response.ok)
        return response;

    const contentType = response.headers.get("Content-Type") ?? "";
    const message = contentType.includes("json")
        ? await response.json().then(value => typeof value === "string" ? value : value.detail || value.title)
        : contentType.includes("text/plain") ? await response.text() : "";
    throw new Error(message || `Request failed (HTTP ${response.status}).`);
}

function post(path, payload, headers = {}) {
    return request(path, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...headers },
        body: JSON.stringify(payload)
    });
}

async function render(template, model) {
    const response = await post("/render", { template, model });
    return response.text();
}

function showPreview(html, preserveScroll = false) {
    clientPreview.show(html, preserveScroll);
}

function cancelPendingPreview() {
    pendingFrame?.remove();
    pendingFrame = null;
}

function setPreviewHtml(html, preserveScroll, onReady, client) {
    if (html === null) {
        frame.removeAttribute("srcdoc");
        return;
    }
    if (frame.srcdoc === html) {
        frame.dataset.client = client;
        applyPreviewTheme();
        onReady();
        return;
    }

    // Keep the previous email visible until the replacement is ready to paint.
    const next = frame.cloneNode(false);
    next.removeAttribute("id");
    next.classList.add("preview-frame-pending");
    next.inert = true;
    next.dataset.client = client;
    next.srcdoc = html;
    pendingFrame = next;
    next.addEventListener("load", () => {
        if (pendingFrame !== next)
            return;

        const scroll = preserveScroll ? frame.contentDocument?.scrollingElement : null;
        applyPreviewTheme(next);
        if (scroll) {
            next.contentDocument?.scrollingElement?.scrollTo({
                top: scroll.scrollTop,
                left: scroll.scrollLeft,
                behavior: "instant"
            });
        }

        frame.remove();
        next.id = "preview";
        next.inert = false;
        next.classList.remove("preview-frame-pending");
        frame = next;
        pendingFrame = null;
        onReady();
    }, { once: true });
    frame.after(next);
}

function watchCatalog(update, failed) {
    let previousRevision = null;
    let checking = false;

    async function check() {
        if (checking || document.hidden)
            return;

        checking = true;
        try {
            const { templates, revision, canSend, isRunningUnderDotnetWatch } = await (await request("/templates")).json();
            const changed = previousRevision !== null && previousRevision !== revision;
            previousRevision = revision;
            update(templates, changed, canSend, isRunningUnderDotnetWatch);
        } catch {
            failed?.();
        } finally {
            checking = false;
        }
    }

    void check();
    window.setInterval(check, 1500);
    document.addEventListener("visibilitychange", check);
}

function startKitchen() {
    const previewPanel = byId("preview-panel");
    const search = byId("search");
    const list = byId("template-list");
    const model = byId("model");
    const renderButton = byId("render");
    const sendButton = byId("send");
    const sendTooltip = byId("send-tooltip");
    const reset = byId("reset");
    const open = byId("open-preview");
    const openTooltip = byId("open-preview-tooltip");
    const watchWarningTooltip = byId("watch-warning-tooltip");
    const messages = byId("messages");
    const dialog = byId("send-dialog");
    const recipient = byId("recipient");
    const recipientKey = "emailkitchen.recipient";
    const subject = byId("subject");
    const confirmSend = byId("confirm-send");
    const sendStatus = byId("send-status");
    const drafts = new Map();
    let templates = [];
    let selected = null;
    let selectionVersion = 0;
    let sending = null;
    let catalogLoaded = false;
    let senderStatusKnown = false;
    let canSend = false;

    function updateControls() {
        const draft = drafts.get(selected);
        model.disabled = !draft;
        model.setAttribute("aria-invalid", String(Boolean(draft?.validationError)));
        reset.disabled = !draft;
        renderButton.disabled = !draft?.model || draft.rendering;
        sendButton.disabled = !senderStatusKnown || !canSend || !draft?.model || sending !== null;
        const senderMessage = !senderStatusKnown
            ? "Checking whether IEmailSender is registered…"
            : canSend ? "" : "IEmailSender is unavailable, so test email sending is disabled. Register an implementation in dependency injection to enable it.";
        sendTooltip.dataset.tooltip = senderMessage;
        sendTooltip.tabIndex = !canSend || !senderStatusKnown ? 0 : -1;
        sendTooltip.setAttribute("aria-label", senderMessage ? `Send Email unavailable. ${senderMessage}` : "Send Email");
    }

    function updateOpen(draft) {
        if (draft?.rendered) {
            open.href = `${root}/preview/${templatePath(selected)}#${encodeURIComponent(JSON.stringify(draft.rendered.model))}`;
            open.removeAttribute("aria-disabled");
            openTooltip.dataset.tooltip = "";
            openTooltip.tabIndex = -1;
            openTooltip.removeAttribute("aria-label");
        } else {
            open.removeAttribute("href");
            open.setAttribute("aria-disabled", "true");
            openTooltip.dataset.tooltip = "Render this template first to enable Live Preview.";
            openTooltip.tabIndex = 0;
            openTooltip.setAttribute("aria-label", "Open Live Preview unavailable. Render this template first.");
        }
    }

    function showDraft(draft) {
        const preview = draft?.rendered;
        byId("preview-empty").hidden = Boolean(preview);
        if (preview)
            showPreview(preview.html);
        else
            clientPreview.clear();
        updateOpen(draft);
    }

    function setMessage(state, text) {
        messages.dataset.state = state;
        status.textContent = text;
        status.title = text;
    }

    function updateMessage(draft) {
        if (draft?.validationError) {
            setMessage("error", draft.validationError);
        } else if (draft?.rendering) {
            setMessage("busy", "Rendering…");
        } else if (draft?.error) {
            setMessage("error", draft.error);
        } else if (draft?.sentTo) {
            setMessage("ready", `Email sent to ${draft.sentTo}.`);
        } else if (draft?.rendered && JSON.stringify(draft.rendered.model) !== JSON.stringify(draft.model)) {
            setMessage("changed", "Model changed. Render to update the preview.");
        } else if (draft?.rendered) {
            setMessage("ready", "Rendered");
        } else if (draft) {
            setMessage("idle", "Ready to render");
        } else {
            setMessage("idle", selected ? "Loading model…" : "Select a template to get started.");
        }
    }

    function updateSelection() {
        for (const row of list.querySelectorAll("[data-template]"))
            row.setAttribute("aria-pressed", String(row.dataset.template === selected));
    }

    function showList() {
        const query = search.value.trim().toLowerCase();
        const matches = templates.filter(name =>
            name.toLowerCase().includes(query) || templateLabel(name).toLowerCase().includes(query));
        const top = list.scrollTop;
        const focused = document.activeElement?.dataset.template;
        byId("template-count").textContent = String(matches.length);

        if (!matches.length) {
            const message = document.createElement("p");
            message.className = "list-message";
            message.textContent = query ? "No matching templates." : "No email templates found.";
            list.replaceChildren(message);
            return;
        }

        const rows = matches.map(name => {
            const row = document.createElement("button");
            row.type = "button";
            row.className = "template-row";
            row.dataset.template = name;
            row.title = name;
            row.innerHTML = `<span class="template-icon"><svg class="icon"><use href="#icon-mail"></use></svg></span>
                <span><span class="template-name"></span><span class="template-folder"></span></span>`;
            row.querySelector(".template-name").textContent = templateLabel(name);
            row.querySelector(".template-folder").textContent = name.includes("/") ? name.split("/").slice(0, -1).join(" / ") : "";
            return row;
        });
        list.replaceChildren(...rows);
        list.scrollTop = top;
        rows.find(row => row.dataset.template === focused)?.focus({ preventScroll: true });
        updateSelection();
    }

    async function select(name) {
        const version = ++selectionVersion;
        selected = name;
        let draft = drafts.get(name);
        model.value = draft?.source ?? "";
        model.scrollTop = 0;
        byId("preview-name").textContent = name ?? "Select a template";
        showDraft(draft);
        updateMessage(draft);
        updateSelection();
        updateControls();

        if (!name || draft)
            return;

        try {
            const { model: defaults } = await (await request("/model/" + templatePath(name))).json();
            if (version !== selectionVersion)
                return;

            const source = JSON.stringify(defaults, null, 2);
            draft = { source, defaults: source, model: defaults, rendered: null };
            drafts.set(name, draft);
            model.value = source;
            updateMessage(draft);
        } catch (error) {
            if (version === selectionVersion)
                setMessage("error", error.message);
        } finally {
            updateControls();
        }
    }

    function updateModel(draft) {
        draft.source = model.value;
        draft.error = null;
        draft.sentTo = null;
        draft.validationError = null;
        try {
            const value = JSON.parse(model.value);
            if (value === null || typeof value !== "object" || Array.isArray(value))
                throw new Error("The model must be a JSON object.");
            draft.model = value;
        } catch (error) {
            draft.model = null;
            draft.validationError = error.message;
        }
        updateControls();
        updateMessage(draft);
    }

    async function renderSelected(runtime = false) {
        const template = selected;
        const draft = drafts.get(template);
        if (!draft || (runtime && !draft.rendered))
            return;
        if (draft.rendering) {
            if (runtime)
                draft.refreshPending = true;
            return;
        }

        const value = runtime ? draft.rendered.model : draft.model;
        if (!value)
            return;

        draft.rendering = true;
        draft.error = null;
        if (!runtime)
            draft.sentTo = null;
        updateControls();
        updateMessage(draft);

        try {
            const html = await render(template, value);
            draft.rendered = { html, model: value };
            if (selected === template) {
                showPreview(html, runtime);
                byId("preview-empty").hidden = true;
                updateOpen(draft);
                if (!runtime && matchMedia("(max-width: 760px)").matches)
                    previewPanel.scrollIntoView({ behavior: "smooth", block: "start" });
            }
        } catch (error) {
            draft.error = error.message;
        } finally {
            draft.rendering = false;
            if (selected === template) {
                updateControls();
                updateMessage(draft);
                if (draft.refreshPending) {
                    draft.refreshPending = false;
                    void renderSelected(true);
                }
            }
        }
    }

    search.addEventListener("input", showList);
    list.addEventListener("click", event => {
        const row = event.target.closest("[data-template]");
        if (row && (row.dataset.template !== selected || !drafts.has(selected)))
            void select(row.dataset.template);
    });
    model.addEventListener("input", () => updateModel(drafts.get(selected)));
    model.addEventListener("blur", () => {
        const draft = drafts.get(selected);
        if (draft?.model)
            model.value = draft.source = JSON.stringify(draft.model, null, 2);
    });
    model.addEventListener("keydown", event => {
        if (event.key === "Enter" && (event.ctrlKey || event.metaKey) && !renderButton.disabled) {
            event.preventDefault();
            void renderSelected();
        }
    });
    reset.addEventListener("click", () => {
        const draft = drafts.get(selected);
        model.value = draft.defaults;
        updateModel(draft);
        model.focus();
    });
    renderButton.addEventListener("click", () => void renderSelected());
    sendButton.addEventListener("click", () => {
        const value = drafts.get(selected)?.model;
        if (!value)
            return;
        sending = { template: selected, model: value };
        recipient.value = readPreference(recipientKey) ?? recipient.value;
        subject.value = templateLabel(selected);
        showMessage(sendStatus, "");
        dialog.showModal();
        updateControls();
        recipient.focus();
    });

    for (const id of ["close-send", "cancel-send"])
        byId(id).addEventListener("click", () => dialog.close());
    dialog.addEventListener("cancel", event => {
        if (confirmSend.disabled)
            event.preventDefault();
    });
    dialog.addEventListener("close", () => {
        sending = null;
        updateControls();
    });
    byId("send-form").addEventListener("submit", async event => {
        event.preventDefault();
        if (!sending || confirmSend.disabled)
            return;

        const template = sending.template;
        const to = recipient.value.trim();
        savePreference(recipientKey, to);
        const token = document.querySelector('meta[name="antiforgery-token"]').content;
        const controls = [confirmSend, byId("close-send"), byId("cancel-send")];
        controls.forEach(control => control.disabled = true);
        showMessage(sendStatus, "Sending…");

        try {
            await post("/send", { ...sending, to, subject: subject.value.trim() }, { RequestVerificationToken: token });
            const draft = drafts.get(template);
            draft.sentTo = to;
            draft.error = null;
            dialog.close();
            if (selected === template)
                setMessage("ready", `Email sent to ${to}.`);
        } catch (error) {
            showMessage(sendStatus, error.message, true);
        } finally {
            controls.forEach(control => control.disabled = false);
        }
    });
    watchCatalog((names, changed, senderAvailable, runningUnderDotnetWatch) => {
        watchWarningTooltip.hidden = runningUnderDotnetWatch === true;
        senderStatusKnown = typeof senderAvailable === "boolean";
        canSend = senderStatusKnown && senderAvailable;
        updateControls();

        if (!catalogLoaded || JSON.stringify(names) !== JSON.stringify(templates)) {
            catalogLoaded = true;
            templates = names;
            showList();
            if (!names.includes(selected)) {
                void select(names[0] ?? null);
                return;
            }
        }

        if (changed) {
            if (drafts.has(selected))
                void renderSelected(true);
            else
                void select(selected);
        }
    }, () => {
        if (!catalogLoaded) {
            list.querySelector(".list-message").textContent = "Unable to load templates.";
            setMessage("error", "Unable to load templates.");
        }
    });
}

function startStandalonePreview() {
    const watchWarning = byId("watch-warning");
    const backToKitchen = byId("back-to-kitchen");
    backToKitchen.addEventListener("click", event => {
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)
            return;

        event.preventDefault();
        try {
            window.close();
        } catch {
            window.location.assign(backToKitchen.href);
            return;
        }

        window.setTimeout(() => {
            if (!window.closed)
                window.location.assign(backToKitchen.href);
        }, 150);
    });

    const template = location.pathname.slice((root + "/preview/").length).split("/").map(decodeURIComponent).join("/");
    byId("preview-name").textContent = templateLabel(template);
    document.title = `${templateLabel(template)} · Email Kitchen`;
    let busy = false;
    let pending = false;
    let hasRendered = false;
    let isRunningUnderDotnetWatch = null;
    let model;

    function showError(error) {
        status.textContent = error.message;
        status.hidden = false;
        previewState("error", "Render failed");
    }

    try {
        model = JSON.parse(decodeURIComponent(location.hash.slice(1)));
    } catch {
        showError(new Error("Open a rendered template from Email Kitchen."));
        return;
    }

    async function refresh() {
        if (busy) {
            pending = true;
            return;
        }

        busy = true;
        if (!hasRendered)
            previewState("busy", "Rendering…");
        try {
            showPreview(await render(template, model), hasRendered);
            hasRendered = true;
            status.hidden = true;
            previewState(isRunningUnderDotnetWatch ? "ready" : "warning",
                isRunningUnderDotnetWatch ? "Live preview" : "Watch required");
        } catch (error) {
            showError(error);
        } finally {
            busy = false;
            if (pending) {
                pending = false;
                void refresh();
            }
        }
    }

    // Establish a baseline before the first render so edits during rendering are picked up.
    let initialized = false;
    watchCatalog((_, changed, __, runningUnderDotnetWatch) => {
        isRunningUnderDotnetWatch = runningUnderDotnetWatch === true;
        watchWarning.hidden = isRunningUnderDotnetWatch;
        byId("preview-state").hidden = !isRunningUnderDotnetWatch;
        if (!initialized || changed) {
            initialized = true;
            void refresh();
        }
    }, () => {
        if (!hasRendered)
            showError(new Error("Waiting for Email Kitchen to reconnect."));
    });
}

initializeTheme();
initializePreviewWidth();
initializeHeaderOptions();
if (document.body.dataset.page === "kitchen")
    startKitchen();
else
    startStandalonePreview();
