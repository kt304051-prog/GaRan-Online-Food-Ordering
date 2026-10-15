(() => {
    const script = document.currentScript;
    const button = document.getElementById("adminNotificationButton");
    const closeButton = document.getElementById("adminNotificationClose");
    const panel = document.getElementById("adminNotificationPanel");
    const list = document.getElementById("adminNotificationList");
    const browserButton = document.getElementById("adminBrowserNotificationButton");
    const countBadge = document.getElementById("adminNotificationCount");
    const status = document.getElementById("adminNotificationStatus");
    const toastHost = document.getElementById("adminOrderNotifications");
    if (!script || !button || !panel || !list || !countBadge || !toastHost) return;

    const snapshotUrl = script.dataset.snapshotUrl;
    const newOrdersUrl = script.dataset.newOrdersUrl;
    const getDayKey = () => {
        const now = new Date();
        return [now.getFullYear(), String(now.getMonth() + 1).padStart(2, "0"), String(now.getDate()).padStart(2, "0")].join("-");
    };
    const dayKey = getDayKey();
    const storageKey = `garan-admin-order-notifications-${dayKey}`;
    const readKey = `garan-admin-order-notifications-read-${dayKey}`;
    let cursor = 0;
    let unread = 0;
    let busy = false;
    let notifications = [];

    const readStorage = key => {
        try { return sessionStorage.getItem(key); } catch { return null; }
    };
    const writeStorage = (key, value) => {
        try { sessionStorage.setItem(key, value); } catch { /* In-memory notifications remain available. */ }
    };
    const persist = () => {
        writeStorage(storageKey, String(cursor));
        writeStorage(readKey, String(unread));
        writeStorage(`${storageKey}-items`, JSON.stringify(notifications.slice(0, 20)));
        countBadge.textContent = unread > 99 ? "99+" : String(unread);
        countBadge.hidden = unread === 0;
    };

    function renderList() {
        list.replaceChildren();
        if (notifications.length === 0) {
            const empty = document.createElement("div");
            empty.className = "admin-notification-empty";
            empty.textContent = "Chưa có thông báo mới.";
            list.append(empty);
            return;
        }
        for (const order of notifications.slice(0, 20)) {
            const link = document.createElement("a");
            link.className = "admin-notification-item";
            link.href = order.detailsUrl;
            const title = document.createElement("strong");
            title.textContent = `Đơn hàng mới #${order.orderId}`;
            const detail = document.createElement("span");
            detail.textContent = `${order.customerName} · ${Number(order.total).toLocaleString("vi-VN")} đ · ${order.time}`;
            link.append(title, detail);
            link.addEventListener("click", markRead);
            list.append(link);
        }
    }

    function markRead() {
        unread = 0;
        persist();
    }

    function setPanelOpen(open) {
        panel.hidden = !open;
        button.setAttribute("aria-expanded", String(open));
        if (open) markRead();
    }

    button.addEventListener("click", () => setPanelOpen(panel.hidden));
    closeButton?.addEventListener("click", () => setPanelOpen(false));
    document.addEventListener("click", event => {
        if (!panel.hidden && !event.target.closest(".admin-notification-area")) setPanelOpen(false);
    });
    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && !panel.hidden) {
            setPanelOpen(false);
            button.focus();
        }
    });

    function updatePermissionUI() {
        const supported = "Notification" in window;
        if (browserButton) {
            browserButton.hidden = !supported || Notification.permission === "granted";
        }
        if (supported && Notification.permission === "granted") button.classList.add("is-enabled");
    }

    browserButton?.addEventListener("click", async () => {
        if (!("Notification" in window)) {
            status.textContent = "Trình duyệt không hỗ trợ thông báo màn hình.";
            return;
        }
        if (Notification.permission === "denied") {
            status.textContent = "Hãy cho phép thông báo GaRan trong cài đặt trình duyệt.";
            return;
        }
        try {
            const permission = await Notification.requestPermission();
            status.textContent = permission === "granted" ? "Đã bật thông báo màn hình." : "Chưa bật thông báo màn hình.";
            updatePermissionUI();
        } catch {
            status.textContent = "Không bật được thông báo màn hình.";
        }
    });

    function showToast(order) {
        const toast = document.createElement("section");
        toast.className = "admin-order-toast";
        toast.setAttribute("role", "status");
        const heading = document.createElement("strong");
        heading.textContent = `Có đơn hàng mới #${order.orderId}`;
        const detail = document.createElement("span");
        detail.textContent = `${order.customerName} · ${Number(order.total).toLocaleString("vi-VN")} đ · ${order.time}`;
        const actions = document.createElement("div");
        actions.className = "admin-order-toast-actions";
        const openLink = document.createElement("a");
        openLink.href = order.detailsUrl;
        openLink.textContent = "Xem đơn";
        openLink.addEventListener("click", markRead);
        const close = document.createElement("button");
        close.type = "button";
        close.className = "admin-order-toast-close";
        close.setAttribute("aria-label", "Đóng thông báo");
        close.textContent = "×";
        close.addEventListener("click", () => toast.remove());
        actions.append(openLink, close);
        toast.append(heading, detail, actions);
        toastHost.prepend(toast);
        window.setTimeout(() => toast.remove(), 15000);

        if ("Notification" in window && Notification.permission === "granted" && document.hidden) {
            const notification = new Notification(`Đơn hàng mới #${order.orderId}`, {
                body: `${order.customerName} · ${Number(order.total).toLocaleString("vi-VN")} đ`,
                tag: `garan-order-${order.orderId}`
            });
            notification.onclick = () => {
                window.focus();
                window.location.href = order.detailsUrl;
            };
        }
    }

    async function poll() {
        if (getDayKey() !== dayKey) {
            window.location.reload();
            return;
        }
        if (busy) return;
        busy = true;
        try {
            const url = new URL(newOrdersUrl, window.location.origin);
            url.searchParams.set("afterId", String(cursor));
            const response = await fetch(url, { credentials: "same-origin", cache: "no-store" });
            if (!response.ok) return;
            const orders = await response.json();
            if (!Array.isArray(orders) || orders.length === 0) return;
            for (const order of orders) {
                cursor = Math.max(cursor, Number(order.orderId) || 0);
                unread++;
                notifications.unshift(order);
                showToast(order);
            }
            renderList();
            persist();
        } catch {
            // Retry temporary network/database interruptions on the next polling cycle.
        } finally {
            busy = false;
        }
    }

    async function start() {
        const savedCursorValue = readStorage(storageKey);
        const savedCursor = savedCursorValue === null ? Number.NaN : Number(savedCursorValue);
        const savedUnread = Number(readStorage(readKey));
        try {
            const savedItems = JSON.parse(readStorage(`${storageKey}-items`) || "[]");
            if (Array.isArray(savedItems)) notifications = savedItems.slice(0, 20);
        } catch { notifications = []; }
        unread = Number.isFinite(savedUnread) ? Math.max(0, savedUnread) : 0;
        renderList();

        if (Number.isFinite(savedCursor) && savedCursor >= 0) {
            cursor = savedCursor;
        } else {
            try {
                const response = await fetch(snapshotUrl, { credentials: "same-origin", cache: "no-store" });
                if (!response.ok) return;
                const snapshot = await response.json();
                cursor = Number(snapshot.latestOrderId) || 0;
                persist();
            } catch { return; }
        }

        persist();
        await poll();
        window.setInterval(poll, 7000);
    }

    updatePermissionUI();
    start();
})();
