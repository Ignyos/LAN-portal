(function () {
  const year = new Date().getFullYear();
  const yearLine = document.getElementById("year-line");

  if (yearLine) {
    yearLine.textContent = "";
  }

  function getManifestPath() {
    const host = window.location.hostname || "";
    const isDevHost = host.indexOf("dev") !== -1 || host.indexOf("test") !== -1 || host.indexOf("localhost") !== -1;
    return isDevHost ? "./updates/manifest-test.json" : "./updates/manifest.json";
  }

  function isDevHost() {
    const host = window.location.hostname || "";
    return host.indexOf("dev") !== -1 || host.indexOf("test") !== -1 || host.indexOf("localhost") !== -1;
  }

  function formatVersionLabel(version, devHost) {
    if (!version) {
      return devHost ? "latest version" : "latest version";
    }

    const normalized = String(version).trim();
    const match = normalized.match(/^v?(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?$/);
    if (!match) {
      return normalized;
    }

    const [, major, minor, patch, build] = match;
    if (devHost) {
      return `${major}.${minor}.${patch}.${build}`;
    }

    return `${major}.${minor}.${patch}.0`;
  }

  function formatPublishedText(publishedAt, devHost) {
    if (!publishedAt || Number.isNaN(publishedAt.valueOf())) {
      return "unknown publish time";
    }

    return devHost ? publishedAt.toLocaleString() : publishedAt.toLocaleDateString();
  }

  async function wireSingleDownload() {
    const statusEl = document.getElementById("download-status");
    const linkEl = document.getElementById("download-link");
    const checksumEl = document.getElementById("checksum-link");
    const releaseNotesEl = document.getElementById("release-notes-link");

    if (!statusEl || !linkEl) {
      return;
    }

    try {
      const manifestPath = getManifestPath();
      const response = await fetch(manifestPath, { cache: "no-store" });
      if (!response.ok) {
        throw new Error("HTTP " + response.status);
      }

      const manifest = await response.json();
      const version = manifest.version || "unknown";
      const devHost = isDevHost();
      const downloadUrl = manifest.url;
      const checksumUrl = manifest.checksumUrl || (manifest.url ? manifest.url + ".sha256" : "");

      if (!downloadUrl) {
        throw new Error("Manifest missing url");
      }

      const publishedAt = manifest.publishedAt ? new Date(manifest.publishedAt) : null;
      const publishedText = formatPublishedText(publishedAt, devHost);

      statusEl.textContent = "Latest version: " + formatVersionLabel(version, devHost) + publishedText;
      linkEl.textContent = "Download " + formatVersionLabel(version, devHost);
      linkEl.href = downloadUrl;
      linkEl.target = "_blank";
      linkEl.rel = "noopener";
      linkEl.classList.remove("disabled");
      linkEl.removeAttribute("aria-disabled");

      if (checksumEl) {
        checksumEl.textContent = "Checksum (.sha256)";
        checksumEl.href = checksumUrl || "#";
        checksumEl.target = "_blank";
        checksumEl.rel = "noopener";
        checksumEl.classList.remove("disabled");
        checksumEl.removeAttribute("aria-disabled");
      }

      if (releaseNotesEl) {
        releaseNotesEl.textContent = "Release notes";
      }
    } catch (error) {
      statusEl.textContent = "Download unavailable: " + error.message;
      linkEl.textContent = "Download unavailable";
      linkEl.href = "#";
      linkEl.classList.add("disabled");
      linkEl.setAttribute("aria-disabled", "true");

      if (checksumEl) {
        checksumEl.textContent = "Checksum unavailable";
        checksumEl.href = "#";
        checksumEl.classList.add("disabled");
        checksumEl.setAttribute("aria-disabled", "true");
      }
    }
  }

  wireSingleDownload();
})();
