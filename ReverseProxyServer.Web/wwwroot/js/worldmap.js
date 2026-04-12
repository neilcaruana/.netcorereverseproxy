window.worldMap = (() => {
    "use strict";

    let _map = null;
    let _tooltip = null;
    let _datasetId = null;
    let _datasetListener = null;
    let _mapListener = null;
    let _dotNetRef = null;
    let _hoveredCode = null;
    let _values = {};
    let _maxCount = 0;
    let _currentScheme = null;
    let _boundEl = null;
    let _mouseX = 0;
    let _mouseY = 0;
    let _featureTime = 0;
    let _hideTimer = 0;
    let _domClickHandler = null;
    let _mouseDownPos = null;
    let _mouseDownHandler = null;
    let _mouseUpHandler = null;
    let _lastClickTime = 0;
    let _realtimeOverlay = null;
    let _pulses = [];
    let _animFrame = null;

    // Country centroid coordinates (ISO 3166-1 Alpha-2 → [lat, lng])
    const COUNTRY_COORDS = {
        AF:[33,65],AL:[41,20],DZ:[28,3],AD:[42.5,1.5],AO:[-12.5,18.5],AG:[17.05,-61.8],AR:[-34,-64],
        AM:[40,45],AU:[-27,133],AT:[47.3,13.3],AZ:[40.5,47.5],BS:[24.25,-76],BH:[26,50.5],BD:[24,90],
        BB:[13.2,-59.5],BY:[53,28],BE:[50.8,4],BZ:[17.25,-88.75],BJ:[9.5,2.25],BT:[27.5,90.5],
        BO:[-17,-65],BA:[44,18],BW:[-22,24],BR:[-10,-55],BN:[4.5,114.7],BG:[43,25],BF:[13,-2],
        BI:[-3.5,30],KH:[13,105],CM:[6,12],CA:[60,-95],CV:[16,-24],CF:[7,21],TD:[15,19],CL:[-30,-71],
        CN:[35,105],CO:[4,-72],KM:[-12.2,44.25],CG:[-1,15],CD:[-3,23],CR:[10,-84],CI:[8,-5.5],
        HR:[45.2,15.5],CU:[21.5,-80],CY:[35,33],CZ:[49.75,15.5],DK:[56,10],DJ:[11.5,43],DM:[15.4,-61.4],
        DO:[19,-70.7],EC:[-2,-77.5],EG:[27,30],SV:[13.8,-88.9],GQ:[2,10],ER:[15,39],EE:[59,26],
        SZ:[-26.5,31.5],ET:[8,38],FJ:[-18,175],FI:[64,26],FR:[46,2],GA:[-1,11.75],GM:[13.5,-16.5],
        GE:[42,43.5],DE:[51,9],GH:[8,-2],GR:[39,22],GD:[12.1,-61.7],GT:[15.5,-90.25],GN:[11,-10],
        GW:[12,-15],GY:[5,-59],HT:[19,-72.4],HN:[15,-86.5],HU:[47,20],IS:[65,-18],IN:[20,77],
        ID:[-5,120],IR:[32,53],IQ:[33,44],IE:[53,-8],IL:[31.5,34.75],IT:[42.8,12.8],JM:[18.25,-77.5],
        JP:[36,138],JO:[31,36],KZ:[48,68],KE:[1,38],KI:[1.4,173],KP:[40,127],KR:[37,127.5],
        KW:[29.5,47.75],KG:[41,75],LA:[18,105],LV:[57,25],LB:[33.8,35.8],LS:[-29.5,28.5],
        LR:[6.5,-9.5],LY:[25,17],LI:[47.3,9.5],LT:[56,24],LU:[49.75,6.2],MG:[-20,47],MW:[-13.5,34],
        MY:[2.5,112.5],MV:[3.25,73],ML:[17,-4],MT:[35.9,14.4],MH:[9,168],MR:[20,-12],MU:[-20.3,57.6],
        MX:[23,-102],FM:[6.9,158.2],MD:[47,29],MC:[43.7,7.4],MN:[46,105],ME:[42.5,19.3],MA:[32,-5],
        MZ:[-18.25,35],MM:[22,98],NA:[-22,17],NR:[-0.5,166.9],NP:[28,84],NL:[52.5,5.75],NZ:[-42,174],
        NI:[13,-85],NE:[16,8],NG:[10,8],MK:[41.5,22],NO:[62,10],OM:[21,57],PK:[30,70],PW:[7.5,134.5],
        PA:[9,-80],PG:[-6,147],PY:[-23,-58],PE:[-10,-76],PH:[13,122],PL:[52,20],PT:[39.5,-8],
        QA:[25.5,51.25],RO:[46,25],RU:[60,100],RW:[-2,30],KN:[17.3,-62.7],LC:[13.9,-61],
        VC:[13.25,-61.2],WS:[-13.6,-172.3],SM:[43.9,12.4],ST:[1,7],SA:[25,45],SN:[14,-14],
        RS:[44,21],SC:[-4.6,55.5],SL:[8.5,-11.5],SG:[1.4,103.8],SK:[48.7,19.5],SI:[46.1,14.8],
        SB:[-8,159],SO:[10,49],ZA:[-29,24],SS:[7,30],ES:[40,-4],LK:[7,81],SD:[15,30],SR:[4,-56],
        SE:[62,15],CH:[47,8],SY:[35,38],TW:[23.5,121],TJ:[39,71],TZ:[-6,35],TH:[15,100],
        TL:[-8.8,126],TG:[8,1.2],TO:[-20,-175],TT:[11,-61],TN:[34,9],TR:[39,35],TM:[40,60],
        TV:[-8,178],UG:[1,32],UA:[49,32],AE:[24,54],GB:[54,-2],US:[38,-97],UY:[-33,-56],
        UZ:[41,64],VU:[-16,167],VE:[8,-66],VN:[16,106],YE:[15,48],ZM:[-15,30],ZW:[-20,30],
        XK:[42.6,20.9],PS:[31.9,35.2],HK:[22.3,114.2],MO:[22.2,113.5],PR:[18.2,-66.5],
        RE:[-21.1,55.5],GP:[16.2,-61.5],MQ:[14.7,-61],GF:[4,-53],NC:[-22.3,166.5],PF:[-15,-140],
        CW:[12.2,-69],AW:[12.5,-70],SX:[18,63.1]
    };

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

    function setCursor(pointer) {
        if (!_boundEl) return;
        _boundEl.style.cursor = pointer ? "pointer" : "";
    }

    function navigateToCountry(code) {
        console.log("[WorldView] navigateToCountry:", code, "dotNetRef:", !!_dotNetRef, "hasData:", code ? !!_values[code] : false);
        if (!_dotNetRef || !code || !_values[code]) return;
        console.log("[WorldView] ✅ Invoking .NET OnCountryClickedFromMap:", code);
        _dotNetRef.invokeMethodAsync("OnCountryClickedFromMap", code)
            .then(() => console.log("[WorldView] ✅ .NET call succeeded"))
            .catch(err => console.error("[WorldView] ❌ .NET call FAILED:", err));
    }

    function handleDomClick(clientX, clientY) {
        // Debounce: mouseup and click may both fire
        const now = Date.now();
        if (now - _lastClickTime < 300) return;
        _lastClickTime = now;

        console.log("[WorldView] 🖱️ Click. hoveredCode:", _hoveredCode, "dotNetRef:", !!_dotNetRef);
        if (!_dotNetRef || !_hoveredCode || !_values[_hoveredCode]) return;

        navigateToCountry(_hoveredCode);
    }

    function removeDomClickListeners() {
        if (_boundEl) {
            if (_mouseDownHandler) _boundEl.removeEventListener("mousedown", _mouseDownHandler, true);
            if (_mouseUpHandler) _boundEl.removeEventListener("mouseup", _mouseUpHandler, true);
            if (_domClickHandler) _boundEl.removeEventListener("click", _domClickHandler);
        }
    }

    function bindDomListeners(el) {
        if (_boundEl === el) return;
        removeDomClickListeners();

        _boundEl = el;

        // Track mousedown position to distinguish clicks from drags
        _mouseDownHandler = (e) => {
            if (e.button === 0) _mouseDownPos = { x: e.clientX, y: e.clientY };
        };

        _mouseUpHandler = (e) => {
            if (e.button !== 0 || !_mouseDownPos) return;
            const dx = e.clientX - _mouseDownPos.x;
            const dy = e.clientY - _mouseDownPos.y;
            const dist = Math.sqrt(dx * dx + dy * dy);
            _mouseDownPos = null;

            // Only treat as click if mouse didn't move more than 5px (not a drag)
            if (dist <= 5) {
                console.log("[WorldView] mousedown→mouseup click (dist=" + dist.toFixed(1) + "px)");
                handleDomClick(e.clientX, e.clientY);
            }
        };

        // Also keep a regular click handler as backup
        _domClickHandler = (e) => {
            console.log("[WorldView] 🖱️ Standard DOM click event fired");
            handleDomClick(e.clientX, e.clientY);
        };

        // Use capture phase (true) for mousedown/mouseup so we see them before Google Maps
        el.addEventListener("mousedown", _mouseDownHandler, true);
        el.addEventListener("mouseup", _mouseUpHandler, true);
        el.addEventListener("click", _domClickHandler);
        el.addEventListener("mousemove", (e) => { _mouseX = e.clientX; _mouseY = e.clientY; });
        el.addEventListener("mouseleave", () => {
            clearTimeout(_hideTimer);
            if (_tooltip) _tooltip.style.display = "none";
            _hoveredCode = null;
            setCursor(false);
        });

        console.log("[WorldView] DOM mousedown/mouseup/click listeners attached (capture phase for mouse events)");
    }

    function attachDatasetListeners(datasetLayer) {
        _datasetListener = datasetLayer.addListener("mousemove", (e) => {
            clearTimeout(_hideTimer);
            _featureTime = Date.now();
            const attrs = e.features && e.features[0] ? e.features[0].datasetAttributes : null;
            if (!attrs) { _tooltip.style.display = "none"; _hoveredCode = null; setCursor(false); return; }
            const code = attrs["ISO3166-1-Alpha-2"] ? attrs["ISO3166-1-Alpha-2"].toString().toUpperCase() : null;
            const count = code ? (_values[code] || 0) : 0;
            _hoveredCode = (code && count) ? code : null;
            setCursor(!!_hoveredCode);
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
                if (Date.now() - _featureTime > 100) {
                    _tooltip.style.display = "none";
                    _hoveredCode = null;
                    setCursor(false);
                }
            }, 120);
        });
    }

    return {
        init(elementId, mapId, datasetId, colorScheme, countryData, dotNetRef) {
            console.log("[WorldView] init() dotNetRef:", !!dotNetRef);
            _dotNetRef = dotNetRef || _dotNetRef;
            _datasetId = datasetId;
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
                _domClickHandler = null;
                _mouseDownHandler = null;
                _mouseUpHandler = null;
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
            if (!_map) return Promise.resolve();
            _hoveredCode = null;
            setCursor(false);
            if (_tooltip) _tooltip.style.display = "none";

            return new Promise((resolve) => {
                setTimeout(() => {
                    if (!_map) { resolve(); return; }
                    const center = _map.getCenter();
                    const zoom = _map.getZoom();
                    google.maps.event.trigger(_map, "resize");
                    if (center) _map.setCenter(center);

                    // Fractional zoom nudge forces the dataset feature layer to
                    // rebuild its hit-test geometry after display:none→block.
                    _map.setZoom(zoom + 0.01);
                    google.maps.event.addListenerOnce(_map, "idle", () => {
                        if (!_map) { resolve(); return; }
                        _map.setZoom(zoom);

                        google.maps.event.addListenerOnce(_map, "idle", () => {
                            if (!_map) { resolve(); return; }
                            removeMapListeners();
                            _hoveredCode = null;
                            const dl = _datasetId ? _map.getDatasetFeatureLayer(_datasetId) : null;
                            if (dl) attachDatasetListeners(dl);
                            resolve();
                        });
                    });
                }, 150);
            });
        },

        dispose() {
            this.stopRealtime();
            removeMapListeners();
            removeDomClickListeners();
            if (_tooltip) { _tooltip.remove(); _tooltip = null; }
            _map = null;
            _boundEl = null;
            _domClickHandler = null;
            _mouseDownHandler = null;
            _mouseUpHandler = null;
            _mouseDownPos = null;
            _dotNetRef = null;
            _datasetId = null;
            _hoveredCode = null;
            _values = {};
            _maxCount = 0;
            _currentScheme = null;
        },

        getState() {
            if (!_map) return null;
            const c = _map.getCenter();
            return { zoom: _map.getZoom(), lat: c.lat(), lng: c.lng() };
        },

        setState(zoom, lat, lng) {
            if (!_map) return;
            _map.setCenter({ lat, lng });
            _map.setZoom(zoom);
        },

        startRealtime(serverCountryCode) {
            if (_realtimeOverlay) return;
            if (!_map || !window.google) return;

            const serverCoords = COUNTRY_COORDS[(serverCountryCode || "MT").toUpperCase()];
            if (!serverCoords) return;
            const serverLatLng = new google.maps.LatLng(serverCoords[0], serverCoords[1]);

            // Helper OverlayView for MapCanvasProjection
            const helper = new google.maps.OverlayView();
            helper.onAdd = function () {};
            helper.onRemove = function () {};
            helper.draw = function () {};
            helper.setMap(_map);

            // Canvas overlay on the map container
            const mapDiv = _map.getDiv();
            const canvas = document.createElement("canvas");
            canvas.style.cssText = "position:absolute;top:0;left:0;width:100%;height:100%;pointer-events:none;z-index:2;";
            mapDiv.style.position = "relative";
            mapDiv.appendChild(canvas);

            function resizeCanvas() {
                canvas.width = mapDiv.clientWidth;
                canvas.height = mapDiv.clientHeight;
            }
            resizeCanvas();
            const resizeObserver = new ResizeObserver(resizeCanvas);
            resizeObserver.observe(mapDiv);

            _realtimeOverlay = { canvas, resizeObserver, helper, serverLatLng };

            // Quadratic bezier helper: compute point at t
            function bezierPt(p0x, p0y, cpx, cpy, p1x, p1y, t) {
                const u = 1 - t;
                return {
                    x: u * u * p0x + 2 * u * t * cpx + t * t * p1x,
                    y: u * u * p0y + 2 * u * t * cpy + t * t * p1y
                };
            }

            // Draw a quadratic bezier curve from t0 to t1
            function drawBezierSegment(ctx, p0x, p0y, cpx, cpy, p1x, p1y, t0, t1, steps) {
                const start = bezierPt(p0x, p0y, cpx, cpy, p1x, p1y, t0);
                ctx.moveTo(start.x, start.y);
                for (let i = 1; i <= steps; i++) {
                    const t = t0 + (t1 - t0) * (i / steps);
                    const pt = bezierPt(p0x, p0y, cpx, cpy, p1x, p1y, t);
                    ctx.lineTo(pt.x, pt.y);
                }
            }

            const ARC_DURATION = 2500;

            function animate() {
                _animFrame = requestAnimationFrame(animate);
                if (!canvas || !_map) return;
                const proj = helper.getProjection();
                if (!proj) return;

                const ctx = canvas.getContext("2d");
                ctx.clearRect(0, 0, canvas.width, canvas.height);

                const now = Date.now();
                _pulses = _pulses.filter(p => now - p.t < ARC_DURATION);
                if (_pulses.length === 0) return;

                const dest = proj.fromLatLngToContainerPixel(serverLatLng);
                if (!dest) return;

                for (const p of _pulses) {
                    const src = proj.fromLatLngToContainerPixel(p.pos);
                    if (!src) continue;

                    const elapsed = now - p.t;
                    const progress = Math.min(elapsed / ARC_DURATION, 1);

                    // Control point: midpoint offset upward for the arc curve
                    const mx = (src.x + dest.x) / 2;
                    const my = (src.y + dest.y) / 2;
                    const dx = dest.x - src.x;
                    const dy = dest.y - src.y;
                    const dist = Math.sqrt(dx * dx + dy * dy);
                    // Arc height proportional to distance, curving upward
                    const arcHeight = Math.min(dist * 0.35, 150);
                    // Perpendicular offset (always curve upward on screen)
                    const nx = -dy / (dist || 1);
                    const ny = dx / (dist || 1);
                    const cpx = mx + nx * arcHeight;
                    const cpy = my + ny * arcHeight;

                    // Phase 1 (0-0.7): Arc line draws progressively + traveling dot
                    // Phase 2 (0.7-1.0): Line fades, impact pulse at destination
                    const drawPhase = Math.min(progress / 0.7, 1);
                    const fadePhase = progress > 0.7 ? (progress - 0.7) / 0.3 : 0;

                    // Draw the arc trail (from start up to current head position)
                    if (drawPhase > 0) {
                        const trailStart = Math.max(0, drawPhase - 0.35);
                        const alpha = fadePhase > 0 ? 1 - fadePhase : 0.8;
                        ctx.beginPath();
                        drawBezierSegment(ctx, src.x, src.y, cpx, cpy, dest.x, dest.y, trailStart, drawPhase, 30);
                        ctx.strokeStyle = `rgba(0, 200, 83, ${alpha})`;
                        ctx.lineWidth = 2;
                        ctx.stroke();
                    }

                    // Traveling dot (head of the arc)
                    if (drawPhase < 1) {
                        const head = bezierPt(src.x, src.y, cpx, cpy, dest.x, dest.y, drawPhase);
                        ctx.beginPath();
                        ctx.arc(head.x, head.y, 4, 0, Math.PI * 2);
                        ctx.fillStyle = "rgba(0, 230, 64, 0.95)";
                        ctx.fill();
                        // Glow
                        ctx.beginPath();
                        ctx.arc(head.x, head.y, 8, 0, Math.PI * 2);
                        ctx.fillStyle = "rgba(0, 230, 64, 0.25)";
                        ctx.fill();
                    }

                    // Source dot (small, fades)
                    if (progress < 0.5) {
                        const srcAlpha = 1 - progress / 0.5;
                        ctx.beginPath();
                        ctx.arc(src.x, src.y, 3, 0, Math.PI * 2);
                        ctx.fillStyle = `rgba(0, 200, 83, ${srcAlpha * 0.7})`;
                        ctx.fill();
                    }

                    // Impact pulse at destination when arc arrives
                    if (fadePhase > 0) {
                        const impactRadius = 15 * fadePhase;
                        const impactAlpha = 1 - fadePhase;
                        ctx.beginPath();
                        ctx.arc(dest.x, dest.y, impactRadius, 0, Math.PI * 2);
                        ctx.strokeStyle = `rgba(0, 200, 83, ${impactAlpha * 0.8})`;
                        ctx.lineWidth = 2;
                        ctx.stroke();
                        // Inner dot
                        ctx.beginPath();
                        ctx.arc(dest.x, dest.y, 3, 0, Math.PI * 2);
                        ctx.fillStyle = `rgba(0, 230, 64, ${impactAlpha})`;
                        ctx.fill();
                    }
                }

                // Persistent server location marker (subtle)
                if (_pulses.length > 0) {
                    ctx.beginPath();
                    ctx.arc(dest.x, dest.y, 5, 0, Math.PI * 2);
                    ctx.fillStyle = "rgba(0, 200, 83, 0.4)";
                    ctx.fill();
                    ctx.beginPath();
                    ctx.arc(dest.x, dest.y, 2, 0, Math.PI * 2);
                    ctx.fillStyle = "rgba(0, 230, 64, 0.8)";
                    ctx.fill();
                }
            }
            animate();
            console.log("[WorldView] Realtime started, server:", serverCountryCode);
        },

        stopRealtime() {
            if (_animFrame) { cancelAnimationFrame(_animFrame); _animFrame = null; }
            if (_realtimeOverlay) {
                _realtimeOverlay.resizeObserver.disconnect();
                _realtimeOverlay.canvas.remove();
                _realtimeOverlay.helper.setMap(null);
                _realtimeOverlay = null;
            }
            _pulses = [];
            console.log("[WorldView] Realtime stopped");
        },

        addPulse(countryCode) {
            if (!_map || !_realtimeOverlay) {
                console.warn("[WorldView] addPulse: no map or overlay", !!_map, !!_realtimeOverlay);
                return;
            }
            const code = countryCode.toUpperCase();
            const coords = COUNTRY_COORDS[code];
            if (!coords) {
                console.warn("[WorldView] addPulse: unknown country code:", code);
                return;
            }
            _pulses.push({
                pos: new google.maps.LatLng(coords[0], coords[1]),
                t: Date.now()
            });
            console.log("[WorldView] Arc added:", code, "→ server, total active:", _pulses.length);
        }
    };
})();
