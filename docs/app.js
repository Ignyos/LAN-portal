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

  async function wireSingleDownload() {
    const linkEl = document.getElementById("download-link");
    const checksumEl = document.getElementById("checksum-link");
    const releaseNotesEl = document.getElementById("release-notes-link");

    if (!linkEl) {
      return;
    }

    try {
      const manifestPath = getManifestPath();
      const response = await fetch(manifestPath, { cache: "no-store" });
      if (!response.ok) {
        throw new Error("HTTP " + response.status);
      }

      const manifest = await response.json();
      const downloadUrl = manifest.url;
      const checksumUrl = manifest.checksumUrl || (manifest.url ? manifest.url + ".sha256" : "");

      if (!downloadUrl) {
        throw new Error("Manifest missing url");
      }

      linkEl.textContent = "Download LAN Portal";
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
