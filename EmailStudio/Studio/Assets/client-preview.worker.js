import { analyzeEmail, simulateDarkMode, transformForClient } from "./emailens-engine.js";

let previousHtml = null;
let compatibilityWarnings = [];
const previews = new Map();

self.addEventListener("message", ({ data }) => {
    try {
        if (data.html !== previousHtml) {
            previews.clear();
            compatibilityWarnings = analyzeEmail(data.html);
            previousHtml = data.html;
        }
        const key = `${data.client}:${data.theme}`;
        let result = previews.get(key);
        if (!result) {
            const transformed = transformForClient(data.html, data.client);
            const themed = data.theme === "dark" ? simulateDarkMode(transformed.html, data.client) : transformed;
            const warnings = [...compatibilityWarnings, ...transformed.warnings, ...themed.warnings].filter(
                (warning) => warning.client === data.client
            );
            const unique = new Map(
                warnings.map((warning) => [
                    `${warning.severity}:${warning.property}:${warning.message}:${warning.line ?? ""}`,
                    warning
                ])
            );
            result = { html: themed.html, warnings: [...unique.values()] };
            previews.set(key, result);
        }
        self.postMessage({ id: data.id, ...result });
    } catch (error) {
        self.postMessage({ id: data.id, error: error.message || "The client preview could not be generated." });
    }
});
