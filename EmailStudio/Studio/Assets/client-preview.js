import { clientProfiles } from "./client-profiles.js";

export function createClientPreview({ surface, onRender, cancelPendingRender, readPreference, savePreference }) {
    const key = "emailstudio.preview-client";
    const select = document.getElementById("preview-client");
    const toolbar = document.getElementById("client-toolbar");
    const badge = document.getElementById("client-badge");
    const description = document.getElementById("client-description");
    const notes = document.getElementById("compatibility-notes");
    const count = document.getElementById("compatibility-count");
    const list = document.getElementById("compatibility-list");
    const groups = [
        ["Outlook", "outlook-"],
        ["Gmail", "gmail-"],
        ["Apple Mail", "apple-mail-"],
        ["Other email apps", null]
    ];
    const names = new Map(clientProfiles.map((profile) => [profile.id, profile.name]));
    let originalHtml = null;
    let selected = names.has(readPreference(key)) ? readPreference(key) : "browser";
    let theme = "light";
    let revision = 0;
    let worker = null;
    let busy = false;

    for (const [label, prefix] of groups) {
        const group = document.createElement("optgroup");
        group.label = label;
        const matches = clientProfiles.filter((profile) =>
            prefix
                ? profile.id.startsWith(prefix)
                : !groups.some(([, candidate]) => candidate && profile.id.startsWith(candidate))
        );
        if (prefix === "outlook-")
            matches.sort(
                (a, b) => Number(b.id === "outlook-windows-legacy") - Number(a.id === "outlook-windows-legacy")
            );
        for (const profile of matches) group.append(new Option(profile.name, profile.id));
        select.append(group);
    }
    select.value = selected;

    function updateNotice(state = selected === "browser" ? "original" : "simulation") {
        toolbar.dataset.state = state;
        const labels = {
            original: "Original",
            simulation: "Simulation",
            busy: "Working",
            error: "Fallback"
        };
        badge.textContent = labels[state];
        description.textContent =
            state === "error"
                ? "Simulation could not be generated. Showing the original HTML in your browser."
                : selected === "browser"
                  ? "Original HTML in your browser. Choose an email app to explore its rendering rules."
                  : `${names.get(selected)} simulation — an approximation, not a screenshot from the actual app. Rendering may differ.`;
        surface.setAttribute("aria-busy", String(state === "busy"));
    }

    function showWarnings(warnings) {
        list.replaceChildren();
        const empty = warnings.length === 0;
        if (empty) notes.open = false;
        notes.dataset.empty = String(empty);
        notes.setAttribute("aria-hidden", String(empty));
        notes.querySelector("summary").tabIndex = empty ? -1 : 0;
        count.textContent = String(warnings.length);
        for (const warning of warnings) {
            const item = document.createElement("li");
            item.dataset.severity = warning.severity;
            const heading = document.createElement("div");
            heading.className = "compatibility-heading";
            const severity = document.createElement("span");
            severity.className = "compatibility-severity";
            severity.textContent =
                warning.severity === "error" ? "Issue" : warning.severity === "info" ? "Note" : "Warning";
            const property = document.createElement("strong");
            property.textContent = warning.property;
            heading.append(severity, property);
            if (warning.line) {
                const line = document.createElement("span");
                line.className = "compatibility-line";
                line.textContent = `HTML line ${warning.line}`;
                heading.append(line);
            }
            const message = document.createElement("p");
            message.textContent = warning.message;
            item.append(heading, message);
            if (warning.suggestion) {
                const suggestion = document.createElement("p");
                suggestion.className = "compatibility-suggestion";
                suggestion.textContent = warning.suggestion;
                item.append(suggestion);
            }
            list.append(item);
        }
    }

    function closeNotes() {
        notes.open = false;
        notes.querySelector("summary")?.focus({ preventScroll: true });
    }

    notes.querySelector(".compatibility-close")?.addEventListener("click", closeNotes);
    notes.querySelector(".compatibility-backdrop")?.addEventListener("click", closeNotes);
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && notes.open) closeNotes();
    });

    function stopWorker() {
        worker?.terminate();
        worker = null;
        busy = false;
    }

    function commit(html, preserveScroll, state, warnings = []) {
        onRender(html, preserveScroll, () => {
            updateNotice(state);
            showWarnings(warnings);
        });
    }

    function render(preserveScroll = true) {
        const id = ++revision;
        cancelPendingRender();
        if (originalHtml === null) {
            updateNotice();
            return;
        }
        updateNotice("busy");
        if (selected === "browser") {
            stopWorker();
            commit(originalHtml, preserveScroll, "original");
            return;
        }
        // A different selection or HTML can cancel expensive work immediately.
        if (busy) stopWorker();
        if (!worker) {
            try {
                worker = new Worker(new URL("./client-preview.worker.js", import.meta.url), {
                    type: "module",
                    name: "EmailStudio client preview"
                });
            } catch {
                fail(preserveScroll);
                return;
            }
        }
        busy = true;
        worker.onmessage = ({ data }) => {
            if (data.id !== revision) return;
            busy = false;
            if (data.error) {
                fail(preserveScroll);
                return;
            }
            commit(data.html, preserveScroll, "simulation", data.warnings);
        };
        worker.onerror = (event) => {
            event.preventDefault();
            if (id === revision) fail(preserveScroll);
        };
        worker.postMessage({ id, html: originalHtml, client: selected, theme });
    }

    function fail(preserveScroll) {
        stopWorker();
        commit(originalHtml, preserveScroll, "error");
    }

    function selectClient(value, persist = false) {
        selected = names.has(value) ? value : "browser";
        select.value = selected;
        if (persist) savePreference(key, selected);
        render();
    }

    select.addEventListener("change", () => selectClient(select.value, true));
    window.addEventListener("storage", (event) => {
        if (event.key === key || event.key === null) selectClient(event.key === null ? null : event.newValue);
    });
    window.addEventListener("pagehide", stopWorker);
    window.addEventListener("pageshow", (event) => {
        if (event.persisted) render();
    });
    updateNotice();

    return {
        show(html, preserveScroll = false) {
            originalHtml = html;
            render(preserveScroll);
        },
        setTheme(value) {
            if (theme === value) return;
            theme = value;
            render();
        },
        clear() {
            revision++;
            originalHtml = null;
            stopWorker();
            cancelPendingRender();
            showWarnings([]);
            updateNotice();
            onRender(null);
        }
    };
}
