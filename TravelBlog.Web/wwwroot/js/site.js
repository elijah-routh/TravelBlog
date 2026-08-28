document.addEventListener("DOMContentLoaded", () => {
    const toastElement = document.querySelector("[data-verification-toast]");
    if (!toastElement || typeof bootstrap === "undefined") {
        return;
    }

    const fingerprint = toastElement.dataset.verificationFingerprint;
    if (!fingerprint) {
        return;
    }

    const storageKey = `travelblog.verification-toast.dismissed:${fingerprint}`;
    try {
        if (window.localStorage.getItem(storageKey) === "1") {
            return;
        }
    } catch {
        // Storage can be unavailable in private or restricted browser contexts.
    }

    toastElement.addEventListener("hidden.bs.toast", () => {
        try {
            window.localStorage.setItem(storageKey, "1");
        } catch {
            // Dismissal remains effective for the current page.
        }
    }, { once: true });

    bootstrap.Toast.getOrCreateInstance(toastElement, {
        autohide: false
    }).show();
});
