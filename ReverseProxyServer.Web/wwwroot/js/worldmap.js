window.worldMap = (() => {
    "use strict";

    let _map = null;
    let _tooltip = null;
    let _datasetListener = null;
    let _mapListener = null;
    let _values = {};
    let _maxCount = 0;
    let _currentScheme = null;
    let _boundEl = null;
    let _mouseX = 0;
    let _mouseY = 0;
    let _featureTime = 0;
    let _hideTimer = 0;

    const TOOLTIP_CSS = "position:fixed;pointer-events:none;z-index:9999;display:none;"
        + "background:#fff;border:1px solid #dee2e6;border-radius:0.375rem;"
        + "box-shadow:0 0.125rem 0.5rem rgba(0,0,0,0.15);padding:0.4rem 0.65rem;"
        + "font-family:inherit;font-size:0.75rem;line-height:1.4;max-width:220px;";

    const HEAT_STOPS = [
        [0.0, [0xff, 0xff, 0xb2]],
        [0.25, [0xfe, 0xcc, 0x5c]],
        [0.5, [0xfd, 0x8d, 0x3c]],
        [0.75, [0xf0, 0x3b, 0x20]],
        [1.0, [0xbd, 0x00, 0x26]]
    ];

    function getHeatColor(count) {
        if (!count || _maxCount === 0) return null;
        const ratio = Math.log(count + 1) / Math.log(_maxCount + 1);
        let i = 1;
        while (i < HEAT_STOPS.length - 1 && ratio > HEAT_STOPS[i][0]) i++;
        const [r0, c0] = HEAT_STOPS[i - 1];
        const [r1, c1] = HEAT_STOPS[i];
        const t = (ratio - r0) / (r1 - r0);
        return "rgb(" +
            Math.round(c0[0] + (c1[0] - c0[0]) * t) + "," +
            Math.round(c0[1] + (c1[1] - c0[1]) * t) + "," +
            Math.round(c0[2] + (c1[2] - c0[2]) * t) + ")";
    }

    function ensureTooltip() {
        if (_tooltip) return;
        _tooltip = document.createElement("div");
        _tooltip.style.cssText = TOOLTIP_CSS;
        document.body.appendChild(_tooltip);
    }

    function removeMapListeners() {
        if (_datasetListener) { google.maps.event.removeListener(_datasetListener); _datasetListener = null; }
        if (_mapListener) { google.maps.event.removeListener(_mapListener); _mapListener = null; }
    }

    function bindDomListeners(el) {
        if (_boundEl === el) return;
        _boundEl = el;
        el.addEventListener("mousemove", (e) => { _mouseX = e.clientX; _mouseY = e.clientY; });
        el.addEventListener("mouseleave", () => {
            clearTimeout(_hideTimer);
            if (_tooltip) _tooltip.style.display = "none";
        });
    }

    function attachDatasetListeners(datasetLayer) {
        _datasetListener = datasetLayer.addListener("mousemove", (e) => {
            clearTimeout(_hideTimer);
            _featureTime = Date.now();
            const attrs = e.features && e.features[0] ? e.features[0].datasetAttributes : null;
            if (!attrs) { _tooltip.style.display = "none"; return; }
            const code = attrs["ISO3166-1-Alpha-2"] ? attrs["ISO3166-1-Alpha-2"].toString().toUpperCase() : null;
            const count = code ? (_values[code] || 0) : 0;
            if (!count) { _tooltip.style.display = "none"; return; }
            const name = attrs["ADMIN"] || attrs["NAME"] || attrs["name"] || code || "Unknown";
            const flag = code
                ? '<img src="https://flagcdn.com/w20/' + code.toLowerCase() + '.png" width="14" height="10" alt="" style="vertical-align:middle;margin-right:4px" />'
                : "";
            _tooltip.innerHTML = '<div style="font-weight:600;margin-bottom:2px">' + flag + name + "</div>" +
                '<span style="color:#6c757d">' + count.toLocaleString() + " connections</span>";
            _tooltip.style.display = "block";
            const mx = (e.domEvent && e.domEvent.clientX) ? e.domEvent.clientX : _mouseX;
            const my = (e.domEvent && e.domEvent.clientY) ? e.domEvent.clientY : _mouseY;
            _tooltip.style.left = (mx + 14) + "px";
            _tooltip.style.top = (my + 14) + "px";
        });

        _mapListener = _map.addListener("mousemove", () => {
            clearTimeout(_hideTimer);
            _hideTimer = setTimeout(() => {
                if (Date.now() - _featureTime > 100) _tooltip.style.display = "none";
            }, 120);
        });
    }

    return {
        init(elementId, mapId, datasetId, colorScheme, countryData) {
            const el = document.getElementById(elementId);
            if (!el || !window.google || !google.maps) {
                console.warn("[WorldView] Google Maps not ready yet.");
                return false;
            }

            _values = countryData || {};
            _maxCount = 0;
            for (const k in _values) { if (_values[k] > _maxCount) _maxCount = _values[k]; }

            const needsRecreate = !_map || _currentScheme !== colorScheme
                || !document.contains(_map.getDiv());

            if (needsRecreate) {
                removeMapListeners();
                if (_tooltip) _tooltip.style.display = "none";
                el.innerHTML = "";
                _currentScheme = colorScheme;
                _boundEl = null;
                _map = new google.maps.Map(el, {
                    center: { lat: 20, lng: 0 },
                    zoom: 2,
                    minZoom: 2,
                    maxZoom: 12,
                    mapId: mapId,
                    colorScheme: colorScheme,
                    gestureHandling: "greedy",
                    streetViewControl: false,
                    fullscreenControl: false,
                    mapTypeControl: false,
                    keyboardShortcuts: false
                });
            }

            const datasetLayer = _map.getDatasetFeatureLayer(datasetId);
            if (!datasetLayer) {
                console.error("[WorldView] Dataset feature layer not available for ID:", datasetId);
                return false;
            }

            datasetLayer.style = (params) => {
                const attrs = params.feature.datasetAttributes;
                const code = attrs && attrs["ISO3166-1-Alpha-2"]
                    ? attrs["ISO3166-1-Alpha-2"].toString().toUpperCase() : null;
                const count = code ? (_values[code] || 0) : 0;
                const color = getHeatColor(count);
                return {
                    fillColor: color || "#e8e8e8",
                    fillOpacity: count ? 0.8 : 0.05,
                    strokeColor: "#adb5bd",
                    strokeWeight: 0.4
                };
            };

            ensureTooltip();
            removeMapListeners();
            bindDomListeners(el);
            attachDatasetListeners(datasetLayer);

            console.log("[WorldView] Initialized.", Object.keys(_values).length, "countries, max:", _maxCount);
            return true;
        },

        resize() {
            if (!_map) return;
            setTimeout(() => {
                if (!_map) return;
                const c = _map.getCenter();
                google.maps.event.trigger(_map, "resize");
                if (c) _map.setCenter(c);
            }, 100);
        },

        dispose() {
            removeMapListeners();
            if (_tooltip) { _tooltip.remove(); _tooltip = null; }
            _map = null;
            _boundEl = null;
            _values = {};
            _maxCount = 0;
            _currentScheme = null;
        }
    };
})();
