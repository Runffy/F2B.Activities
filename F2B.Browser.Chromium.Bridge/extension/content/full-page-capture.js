(function (root) {
  function captureFullPageAsDataUrl() {
    return new Promise((resolve, reject) => {
      if (typeof html2canvas !== 'function') {
        reject(new Error('html2canvas is not available in the page context.'));
        return;
      }

      const target = document.body || document.documentElement;
      if (!target) {
        reject(new Error('Full-page screenshot failed: document body is not available.'));
        return;
      }

      const scrollX = window.scrollX || window.pageXOffset || 0;
      const scrollY = window.scrollY || window.pageYOffset || 0;

      window.scrollTo(0, 0);

      const runCapture = () => {
        html2canvas(target, {
          scale: Math.min(2, window.devicePixelRatio || 1),
          useCORS: true,
          allowTaint: true,
          backgroundColor: '#ffffff',
          scrollX: 0,
          scrollY: 0,
          logging: false
        }).then((canvas) => {
          window.scrollTo(scrollX, scrollY);
          resolve(canvas.toDataURL('image/png'));
        }).catch((error) => {
          window.scrollTo(scrollX, scrollY);
          reject(error);
        });
      };

      setTimeout(runCapture, 100);
    });
  }

  root.__f2bCaptureFullPage = captureFullPageAsDataUrl;
})(typeof globalThis !== 'undefined' ? globalThis : self);
