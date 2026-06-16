window.cashFlowAuthStorage = {
    get(key) {
        const payload = window.localStorage.getItem(key);
        return payload ? JSON.parse(payload) : null;
    },
    set(key, value) {
        window.localStorage.setItem(key, JSON.stringify(value));
    },
    remove(key) {
        window.localStorage.removeItem(key);
    },
    createOAuthState() {
        const bytes = new Uint8Array(16);
        window.crypto.getRandomValues(bytes);
        return Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
    },
    saveOAuthState(state) {
        window.sessionStorage.setItem("cashflow.oauth.state", state);
    },
    validateOAuthState(state) {
        const expected = window.sessionStorage.getItem("cashflow.oauth.state");
        window.sessionStorage.removeItem("cashflow.oauth.state");
        return expected && expected === state;
    }
};