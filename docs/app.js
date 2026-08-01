(function () {
  const year = new Date().getFullYear();
  const yearLine = document.getElementById("year-line");

  if (yearLine) {
    yearLine.textContent = "Pages shell initialized " + year;
  }

  async function wireManifestDownload(options) {
    const statusEl = document.getElementById(options.statusId);
    const linkEl = document.getElementById(options.linkId);

    if (!statusEl || !linkEl) {
      return;
    }

    try {
      const response = await fetch(options.manifestPath, { cache: "no-store" });
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

      statusEl.textContent = "Latest " + options.channelLabel + " build: " + version + " (" + publishedText + ")";
      linkEl.textContent = "Download " + version;
      linkEl.href = downloadUrl;
      linkEl.target = "_blank";
      linkEl.rel = "noopener";
      linkEl.classList.remove("disabled");
      linkEl.removeAttribute("aria-disabled");
    } catch (error) {
      statusEl.textContent = "Manifest unavailable: " + error.message;
      linkEl.textContent = "Manifest unavailable";
      linkEl.href = "#";
      linkEl.classList.add("disabled");
      linkEl.setAttribute("aria-disabled", "true");
    }
  }

  wireManifestDownload({
    statusId: "prod-status",
    linkId: "prod-download",
    manifestPath: "./updates/manifest.json",
    channelLabel: "production"
  });

  wireManifestDownload({
    statusId: "test-status",
    linkId: "test-download",
    manifestPath: "./updates/manifest-test.json",
    channelLabel: "test"
  });
})();
