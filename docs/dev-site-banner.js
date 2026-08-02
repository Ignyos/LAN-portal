(function () {
  'use strict';

  var defaults = {
    enabled: true,
    patterns: ['test-dev'],
    message: 'DEVELOPER PAGE',
    backgroundColor: '#b91c1c',
    textColor: '#ffffff',
    heightPx: 44,
    zIndex: 2147483647,
    skipOnLocalhost: true
  };

  var userConfig = window.DEV_SITE_BANNER_CONFIG || {};
  var config = Object.assign({}, defaults, userConfig);

  if (!config.enabled) {
    return;
  }

  var host = window.location.hostname || '';
  if (config.skipOnLocalhost && (host === 'localhost' || host === '127.0.0.1')) {
    return;
  }

  var patterns = Array.isArray(config.patterns) ? config.patterns : [String(config.patterns || '')];
  var isDevHost = patterns.some(function (pattern) {
    if (!pattern) {
      return false;
    }

    try {
      if (pattern instanceof RegExp) {
        return pattern.test(host);
      }

      var asString = String(pattern);
      if (asString.startsWith('/') && asString.endsWith('/')) {
        var regexBody = asString.slice(1, -1);
        return new RegExp(regexBody, 'i').test(host);
      }

      return host.toLowerCase().indexOf(asString.toLowerCase()) !== -1;
    }
    catch (_error) {
      return false;
    }
  });

  if (!isDevHost) {
    return;
  }

  var banner = document.createElement('div');
  banner.setAttribute('role', 'status');
  banner.setAttribute('aria-live', 'polite');
  banner.textContent = config.message;
  banner.style.position = 'fixed';
  banner.style.top = '0';
  banner.style.left = '0';
  banner.style.right = '0';
  banner.style.height = String(config.heightPx) + 'px';
  banner.style.display = 'flex';
  banner.style.alignItems = 'center';
  banner.style.justifyContent = 'center';
  banner.style.background = config.backgroundColor;
  banner.style.color = config.textColor;
  banner.style.fontFamily = 'system-ui, -apple-system, Segoe UI, sans-serif';
  banner.style.fontWeight = '700';
  banner.style.letterSpacing = '0.08em';
  banner.style.textTransform = 'uppercase';
  banner.style.boxShadow = '0 2px 8px rgba(0, 0, 0, 0.25)';
  banner.style.zIndex = String(config.zIndex);

  document.addEventListener('DOMContentLoaded', function () {
    document.body.style.paddingTop = String(config.heightPx) + 'px';
    document.body.prepend(banner);
  });
})();
