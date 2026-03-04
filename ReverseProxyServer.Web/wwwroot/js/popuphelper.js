window.getElementPosition = function (elementId) {
    const el = document.getElementById(elementId);
    if (!el) return null;
    const rect = el.getBoundingClientRect();
    return [Math.round(rect.bottom + 4), Math.round(rect.left)];
};
