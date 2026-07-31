window.lanPortalAuth = {
    get: () => window.localStorage.getItem('ignyos-lan-portal.auth'),
    set: (value) => window.localStorage.setItem('ignyos-lan-portal.auth', value),
    remove: () => window.localStorage.removeItem('ignyos-lan-portal.auth'),
    getLastPath: () => window.localStorage.getItem('ignyos-lan-portal.last-path'),
    setLastPath: (value) => window.localStorage.setItem('ignyos-lan-portal.last-path', value),
    removeLastPath: () => window.localStorage.removeItem('ignyos-lan-portal.last-path')
};
