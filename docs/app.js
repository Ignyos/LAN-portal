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

  function formatVersionLabel(version) {
    if (!version) {
      return "latest build";
    }

    if (/\d+\.\d+\.\d+\.\d+$/.test(version)) {
      return version;
    }

    return version;
  }

  async function wireSingleDownload() {
    const statusEl = document.getElementById("download-status");
    const linkEl = document.getElementById("download-link");
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
      const downloadUrl = manifest.url;

      if (!downloadUrl) {
        throw new Error("Manifest missing url");
      }

      const publishedAt = manifest.publishedAt ? new Date(manifest.publishedAt) : null;
      const publishedText =
        publishedAt && !Number.isNaN(publishedAt.valueOf())
          ? publishedAt.toLocaleString()
          : "unknown publish time";

      statusEl.textContent = "Latest build: " + formatVersionLabel(version) + " (" + publishedText + ")";
      linkEl.textContent = "Download " + formatVersionLabel(version);
      linkEl.href = downloadUrl;
      linkEl.target = "_blank";
      linkEl.rel = "noopener";
      linkEl.classList.remove("disabled");
      linkEl.removeAttribute("aria-disabled");

      if (releaseNotesEl) {
        releaseNotesEl.textContent = "Release notes";
      }
    } catch (error) {
      statusEl.textContent = "Download unavailable: " + error.message;
      linkEl.textContent = "Download unavailable";
      linkEl.href = "#";
      linkEl.classList.add("disabled");
      linkEl.setAttribute("aria-disabled", "true");
    }
  }

  wireSingleDownload();
})();
