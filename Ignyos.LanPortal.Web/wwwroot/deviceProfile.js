window.lanPortalDevice = {
    getSuggestedDeviceLabel: () => {
        const nav = window.navigator || {};
        const userAgent = (nav.userAgent || '').toLowerCase();
        const maxTouchPoints = Number(nav.maxTouchPoints || 0);
        const hasTouch = maxTouchPoints > 0 || 'ontouchstart' in window;

        const isTabletHint = /ipad|tablet|kindle|silk|playbook/.test(userAgent);
        const isMobileHint = /iphone|ipod|android|mobile|windows phone|blackberry/.test(userAgent);

        if (isTabletHint) {
            return 'Tablet';
        }

        if (isMobileHint) {
            return 'Mobile';
        }

        if (hasTouch && window.innerWidth > 600 && window.innerWidth <= 1024) {
            return 'Tablet';
        }

        if (hasTouch && window.innerWidth <= 600) {
            return 'Mobile';
        }

        return 'Desktop';
    }
};