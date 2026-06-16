window.cashFlowDownload = {
    downloadFromBase64: (fileName, contentType, base64Content) => {
        const link = document.createElement('a');
        link.download = fileName;
        link.href = `data:${contentType};base64,${base64Content}`;
        link.click();
    }
};
