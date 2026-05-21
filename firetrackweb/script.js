const routes = ["home", "docs", "screenshots", "source"];
const app = document.querySelector("#app");
const pages = new Map([...document.querySelectorAll("[data-page]")].map((page) => [page.dataset.page, page]));
const navLinks = [...document.querySelectorAll("[data-route]")];
const center = [10.3157, 123.8854];
const incidentPositions = [
  { id: "bonbon", label: "Bonbon", x: 38, y: 28, lat: "10.3521° N", lng: "123.8712° E" },
  { id: "north-reclamation", label: "North Reclamation", x: 45, y: 22 },
  { id: "mabolo", label: "Mabolo", x: 54, y: 27 },
  { id: "lahug", label: "Lahug", x: 62, y: 21 },
  { id: "banilad", label: "Banilad", x: 65, y: 35 },
  { id: "capitol", label: "Capitol", x: 42, y: 40 },
  { id: "kamputhaw", label: "Kamputhaw", x: 52, y: 45 },
  { id: "talamban", label: "Talamban", x: 59, y: 38 },
  { id: "guadalupe", label: "Guadalupe", x: 64, y: 43 },
  { id: "ermita", label: "Ermita", x: 52, y: 68, lat: "10.2931° N", lng: "123.8951° E" },
  { id: "sudlon", label: "Sudlon I", x: 25, y: 20 }
];
let currentRoute = null;
let leafletMap = null;
let activeSourcePath = null;
const sourcePanels = new Map();
const screenshotRoleOrder = ["LOGIN", "SUPER ADMIN", "BFP", "DSWD", "BARANGAY", "CITIZEN"];
const screenshotItems = [
  { role: "LOGIN", title: "Login Page", file: "all-loginpage.png" },
  { role: "LOGIN", title: "Register Citizen", file: "all-registercitizenpage.png" },
  { role: "LOGIN", title: "Loading Page", file: "all-loadingpage.png" },

  { role: "SUPER ADMIN", title: "System Overview", file: "SuperAdmin-systemOverview.png" },
  { role: "SUPER ADMIN", title: "User Management", file: "SuperAdmin-userManagement.png", flow: "super-admin-operators", flowIndex: 1, flowSize: 2 },
  { role: "SUPER ADMIN", title: "Create New Operator", file: "SuperAdmin-createNewOperator.png", flow: "super-admin-operators", flowIndex: 2, flowSize: 2 },
  { role: "SUPER ADMIN", title: "Audit Trail", file: "SuperAdmin-auditTrail.png" },
  { role: "SUPER ADMIN", title: "System Panel", file: "SuperAdmin-systemPanel.png" },

  { role: "BFP", title: "Interface", file: "bfp-interface.png" },
  { role: "BFP", title: "Map Light Mode", file: "bfp-lightmap.png", flow: "bfp-map-mode", flowIndex: 1, flowSize: 2 },
  { role: "BFP", title: "Map Dark Mode", file: "bfp-darkmap.png", flow: "bfp-map-mode", flowIndex: 2, flowSize: 2 },
  { role: "BFP", title: "Register Fire Incident", file: "bfp-registerfire.png" },
  { role: "BFP", title: "Fire Incident Info", file: "bfp-incidentinfo.png", flow: "bfp-incident-status", flowIndex: 1, flowSize: 2 },
  { role: "BFP", title: "Update Status", file: "bfp-updatestatus.png", flow: "bfp-incident-status", flowIndex: 2, flowSize: 2 },
  { role: "BFP", title: "Active Pin", file: "bfp-activepin.png", flow: "bfp-pin-status", flowIndex: 1, flowSize: 3 },
  { role: "BFP", title: "Under Control Pin", file: "bfp-undercontrolpin.png", flow: "bfp-pin-status", flowIndex: 2, flowSize: 3 },
  { role: "BFP", title: "Fire Out Pin", file: "bfp-fireoutpin.png", flow: "bfp-pin-status", flowIndex: 3, flowSize: 3 },
  { role: "BFP", title: "Fire Out Report", file: "bfp-fireoutreport.png" },
  { role: "BFP", title: "Barangay Live Search", file: "bfp-beforelivesearch.png", flow: "bfp-live-search", flowIndex: 1, flowSize: 2 },
  { role: "BFP", title: "Barangay Live Search Results", file: "bfp-afterlivesearch.png", flow: "bfp-live-search", flowIndex: 2, flowSize: 2 },
  { role: "BFP", title: "Analysis Interface", file: "bfp-analysisinterface.png", flow: "bfp-analysis-toggles", flowIndex: 1, flowSize: 3 },
  { role: "BFP", title: "Labels Toggle", file: "bfp-analysislabelsoff.png", flow: "bfp-analysis-toggles", flowIndex: 2, flowSize: 3 },
  { role: "BFP", title: "Borders Toggle", file: "bfp-bordersoff.png", flow: "bfp-analysis-toggles", flowIndex: 3, flowSize: 3 },
  { role: "BFP", title: "Barangay Live Search Analysis", file: "bfp-barangayanalysis.png" },
  { role: "BFP", title: "Citizen Report", file: "bfp-citizenreport.png", flow: "bfp-citizen-report", flowIndex: 1, flowSize: 3 },
  { role: "BFP", title: "Report Verification", file: "bfp-reportverification.png", flow: "bfp-citizen-report", flowIndex: 2, flowSize: 3 },
  { role: "BFP", title: "Report Verified", file: "bfp-reportverified.png", flow: "bfp-citizen-report", flowIndex: 3, flowSize: 3 },

  { role: "DSWD", title: "Interface", file: "dswd-interface.png" },
  { role: "DSWD", title: "Relief Distribution", file: "dswd-reliefdistribution.png", flow: "dswd-distribution", flowIndex: 1, flowSize: 2 },
  { role: "DSWD", title: "Record Distribution", file: "dswd-recorddistribution.png", flow: "dswd-distribution", flowIndex: 2, flowSize: 2 },
  { role: "DSWD", title: "Gaps and Duplicates", file: "dswd-gapsduplicates.png" },
  { role: "DSWD", title: "Active Incidents", file: "dswd-activeincidents.png", flow: "dswd-incident-status", flowIndex: 1, flowSize: 2 },
  { role: "DSWD", title: "Update DSWD Status", file: "dswd-updatedswdstatus.png", flow: "dswd-incident-status", flowIndex: 2, flowSize: 2 },
  { role: "DSWD", title: "City Level Centers", file: "dswd-citylevelcenters.png" },
  { role: "DSWD", title: "Place City Level Center", file: "dswd-placingnewcitycenter.png" },
  { role: "DSWD", title: "Center Info", file: "dswd-citycenterinfo.png", flow: "dswd-center-occupancy", flowIndex: 1, flowSize: 3 },
  { role: "DSWD", title: "Center Occupancy", file: "dswd-updatecenteroccupancy.png", flow: "dswd-center-occupancy", flowIndex: 2, flowSize: 3 },
  { role: "DSWD", title: "Updated Occupancy", file: "dswd-updatedoccupancy.png", flow: "dswd-center-occupancy", flowIndex: 3, flowSize: 3 },
  { role: "DSWD", title: "Center Usage Report", file: "dswd-centerviewusagereport.png" },
  { role: "DSWD", title: "DSWD Analysis", file: "dswd- dswdanalysis.png" },

  { role: "BARANGAY", title: "Interface", file: "barangay-interface.png" },
  { role: "BARANGAY", title: "Affected Families", file: "barangay-affectedfamilies.png" },
  { role: "BARANGAY", title: "Register Family", file: "barangay-registerfamily.png", flow: "barangay-family-center", flowIndex: 1, flowSize: 3 },
  { role: "BARANGAY", title: "Registered Family", file: "barangay-registeredfamily.png", flow: "barangay-family-center", flowIndex: 2, flowSize: 3 },
  { role: "BARANGAY", title: "Assign Center", file: "barangay-assigncenter.png", flow: "barangay-family-center", flowIndex: 3, flowSize: 3 },
  { role: "BARANGAY", title: "Evacuation Centers", file: "barangay-evaccenters.png" },
  { role: "BARANGAY", title: "Evac Map", file: "barangay-evaccentermap.png" },
  { role: "BARANGAY", title: "Place New Center", file: "barangay-placenewcenter.png" },
  { role: "BARANGAY", title: "Center Info", file: "barangay-centerinfo.png", flow: "barangay-choose-center", flowIndex: 1, flowSize: 2 },
  { role: "BARANGAY", title: "Choose Center", file: "barangay-ifchoosecenter.png", flow: "barangay-choose-center", flowIndex: 2, flowSize: 2 },
  { role: "BARANGAY", title: "Request External", file: "barangay-requestexternalmode.png" },
  { role: "BARANGAY", title: "Request Neighbor Barangay Center", file: "barangay-requestneighbouringbarangaycenter.png", flow: "barangay-external-request", flowIndex: 1, flowSize: 2 },
  { role: "BARANGAY", title: "My Request", file: "barangay-myrequest.png", flow: "barangay-external-request", flowIndex: 2, flowSize: 2 },
  { role: "BARANGAY", title: "Incoming Request", file: "barangay-incomingrequests.png", flow: "barangay-request-approval", flowIndex: 1, flowSize: 3 },
  { role: "BARANGAY", title: "Approved Center", file: "barangay-approvedcenter.png", flow: "barangay-request-approval", flowIndex: 2, flowSize: 3 },
  { role: "BARANGAY", title: "Approved Center Shown", file: "barangay-approvedcentershown.png", flow: "barangay-request-approval", flowIndex: 3, flowSize: 3 },
  { role: "BARANGAY", title: "View Center Details", file: "barangay-viewcenterdetails.png" },
  { role: "BARANGAY", title: "Incident Reports", file: "barangay-incidentreports.png" },
  { role: "BARANGAY", title: "Barangay Analysis", file: "barangay-barangay analysis.png" },

  { role: "CITIZEN", title: "Interface", file: "citizen-interface.png" },
  { role: "CITIZEN", title: "Incident Feed", file: "citizen-incidentfeed.png" },
  { role: "CITIZEN", title: "Report Fire", file: "citizen-reportfireincident.png" },
  { role: "CITIZEN", title: "Relief Info", file: "citizen-reliefinfo.png" },
  { role: "CITIZEN", title: "Household Name Does Not Exist", file: "citizen-ifhouseholdnamedoesntexist.png", flow: "citizen-household-missing", flowIndex: 1, flowSize: 2 },
  { role: "CITIZEN", title: "Household Name Does Not Exist Shown", file: "citizen-ifhouseholdnamedoesntexistshown.png", flow: "citizen-household-missing", flowIndex: 2, flowSize: 2 },
  { role: "CITIZEN", title: "Household Name Exists", file: "citizen-ifhouseholdnameexist.png", flow: "citizen-household-found", flowIndex: 1, flowSize: 2 },
  { role: "CITIZEN", title: "Household Name Exists Shown", file: "citizen-ifhouseholdnameexistshown.png", flow: "citizen-household-found", flowIndex: 2, flowSize: 2 },
  { role: "CITIZEN", title: "Message DSWD", file: "citizen-messagedswd.png", flow: "citizen-message-dswd", flowIndex: 1, flowSize: 2 },
  { role: "CITIZEN", title: "Message DSWD Shown", file: "citizen-messagedswdshown.png", flow: "citizen-message-dswd", flowIndex: 2, flowSize: 2 },
  { role: "CITIZEN", title: "DSWD Messages", file: "citizen- dswdcitizenmessages.png", flow: "citizen-dswd-messages", flowIndex: 1, flowSize: 2 },
  { role: "CITIZEN", title: "DSWD Messages Approved", file: "citizen- dswdcitizenmessagesapproved.png", flow: "citizen-dswd-messages", flowIndex: 2, flowSize: 2 },
];

function preferredRoute() {
  const hash = window.location.hash.replace(/^#\/?/, "");
  return routes.includes(hash) ? hash : "home";
}

function setActive(route) {
  navLinks.forEach((link) => {
    const active = link.dataset.route === route;
    link.classList.toggle("active", active);
    if (active) {
      link.setAttribute("aria-current", "page");
    } else {
      link.removeAttribute("aria-current");
    }
  });
}

function showRoute(route, animate = true) {
  if (!routes.includes(route)) route = "home";
  if (route === currentRoute) return;

  const finishSwap = () => {
    pages.forEach((page, key) => {
      page.hidden = key !== route;
    });
    currentRoute = route;
    setActive(route);
    document.body.classList.toggle("source-mode", route === "source");

    if (route === "home") {
      window.setTimeout(() => leafletMap?.invalidateSize(), 80);
    }

    if (animate) {
      app.classList.remove("is-exiting");
      app.classList.add("is-entering");
      window.setTimeout(() => app.classList.remove("is-entering"), 420);
    }
  };

  if (!animate || currentRoute === null) {
    finishSwap();
    return;
  }

  app.classList.add("is-exiting");
  window.setTimeout(finishSwap, 210);
}

function navigate(route) {
  const nextHash = `#${route}`;
  if (window.location.hash !== nextHash) {
    window.location.hash = nextHash;
  } else {
    showRoute(route);
  }
}

function initRouter() {
  navLinks.forEach((link) => {
    link.addEventListener("click", (event) => {
      event.preventDefault();
      navigate(link.dataset.route);
    });
  });

  window.addEventListener("hashchange", () => showRoute(preferredRoute()));
  showRoute(preferredRoute(), false);
}

function initDocsJumps() {
  const docsPanel = document.querySelector('[data-page="docs"] .content-scroll');
  const jumps = [...document.querySelectorAll(".docs-jump")];
  const sections = [...document.querySelectorAll('[data-page="docs"] .doc-section')];
  let activeSectionId = "";
  let scrollFrame = 0;

  const setActiveJump = (sectionId) => {
    if (!sectionId || sectionId === activeSectionId) return;
    activeSectionId = sectionId;

    jumps.forEach((jump) => {
      const active = jump.getAttribute("href") === `#${sectionId}`;
      jump.classList.toggle("active", active);
      if (active) {
        jump.setAttribute("aria-current", "true");
      } else {
        jump.removeAttribute("aria-current");
      }
    });
  };

  const updateActiveFromScroll = () => {
    scrollFrame = 0;
    if (!docsPanel || sections.length === 0) return;

    const activationLine = docsPanel.scrollTop + 60;
    let currentSection = sections[0];

    sections.forEach((section) => {
      if (section.offsetTop <= activationLine) {
        currentSection = section;
      }
    });

    setActiveJump(currentSection.id);
  };

  const queueActiveUpdate = () => {
    if (scrollFrame) return;
    scrollFrame = window.requestAnimationFrame(updateActiveFromScroll);
  };

  jumps.forEach((link) => {
    link.addEventListener("click", (event) => {
      event.preventDefault();
      const target = document.querySelector(link.getAttribute("href"));
      if (!target || !docsPanel) return;
      setActiveJump(target.id);
      docsPanel.scrollTo({
        top: target.offsetTop - 30,
        behavior: "smooth",
      });
    });
  });

  docsPanel?.addEventListener("scroll", queueActiveUpdate, { passive: true });
  window.addEventListener("resize", queueActiveUpdate);

  if ("IntersectionObserver" in window && sections.length > 0) {
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((entry) => entry.isIntersecting)) {
        queueActiveUpdate();
      }
    }, {
      root: docsPanel,
      threshold: 0.3,
      rootMargin: "-60px 0px -60% 0px",
    });

    sections.forEach((section) => observer.observe(section));
  }

  queueActiveUpdate();
}

function initClock() {
  const clock = document.querySelector("#clock");
  if (!clock) return;

  const tick = () => {
    const now = new Date();
    clock.textContent = now.toLocaleTimeString("en-PH", {
      timeZone: "Asia/Manila",
      hour12: false,
    }) + " PST";
  };

  tick();
  window.setInterval(tick, 1000);
}

function initLeafletMap() {
  if (!window.L) return;

  leafletMap = L.map("cebu-map", {
    center,
    zoom: 13,
    zoomControl: false,
    attributionControl: false,
    dragging: false,
    touchZoom: false,
    scrollWheelZoom: false,
    doubleClickZoom: false,
    boxZoom: false,
    keyboard: false,
    tap: false,
  });

  L.tileLayer("https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png", {
    maxZoom: 19,
    subdomains: "abcd",
  }).addTo(leafletMap);

  let step = 0;
  window.setInterval(() => {
    step += 0.32;
    const lat = center[0] + Math.sin(step) * 0.0048;
    const lng = center[1] + Math.cos(step * 0.8) * 0.0062;
    leafletMap.panTo([lat, lng], {
      animate: true,
      duration: 4.6,
      easeLinearity: 0.15,
    });
  }, 5200);
}

function initBarangayOrbs() {
  const layer = document.querySelector(".barangay-orbs");
  if (!layer) return;

  layer.innerHTML = incidentPositions.map((point, index) => `
    <div class="barangay-dot" style="--x:${point.x}%;--y:${point.y}%;--delay:${index * -0.7}s">
      <span>${escapeHtml(point.label)}</span>
    </div>
  `).join("");
}

function initIncidentOrbs() {
  const layer = document.querySelector(".incident-orbs");
  if (!layer) return;

  const active = new Map();
  const activeLimit = () => window.matchMedia("(max-width: 680px)").matches ? 2 : 3;
  const randomItem = (items) => items[Math.floor(Math.random() * items.length)];
  const availablePositions = () => incidentPositions.filter((point) => !active.has(point.id));

  const addOrb = (point) => {
    const orb = document.createElement("div");
    orb.className = "incident";
    orb.dataset.point = point.id;
    orb.style.setProperty("--x", `${point.x}%`);
    orb.style.setProperty("--y", `${point.y}%`);
    orb.style.setProperty("--delay", `${(Math.random() * 1.6).toFixed(2)}s`);
    orb.innerHTML = "<b></b>";
    layer.append(orb);
    active.set(point.id, orb);
    orb.getBoundingClientRect();
    orb.classList.add("is-visible");
  };

  const removeOrb = (pointId) => {
    const orb = active.get(pointId);
    if (!orb) return;
    active.delete(pointId);
    orb.classList.remove("is-visible");
    window.setTimeout(() => orb.remove(), 1050);
  };

  const seedOrbs = () => {
    while (active.size < activeLimit()) {
      const next = randomItem(availablePositions());
      if (!next) break;
      addOrb(next);
    }
  };

  seedOrbs();

  window.setInterval(() => {
    const next = randomItem(availablePositions());
    const retiring = randomItem([...active.keys()]);
    if (!next || !retiring) return;
    removeOrb(retiring);
    addOrb(next);
  }, 3000);

  window.addEventListener("resize", () => {
    while (active.size > activeLimit()) {
      removeOrb(randomItem([...active.keys()]));
    }
    seedOrbs();
  });
}

function initScreenshotFilters() {
  const grid = document.querySelector("#screenshot-grid");
  const buttons = [...document.querySelectorAll(".filters button")];
  const lightbox = document.querySelector("#screenshot-lightbox");
  const lightboxImage = document.querySelector("#lightbox-image");
  const lightboxRole = document.querySelector("#lightbox-role");
  const lightboxTitle = document.querySelector("#lightbox-title");
  const closeButton = document.querySelector(".lightbox-close");

  if (!grid) return;

  const renderCard = (item) => {
    const src = `assets/screenshots/${encodeURI(item.file)}`;
    const hasNext = item.flow && item.flowIndex < item.flowSize;
    const startsLine = !item.flow || item.flowIndex === 1;
    const classes = [
      "screen-card",
      hasNext ? "has-next" : "",
      startsLine ? "line-start" : "",
    ].filter(Boolean).join(" ");

    return `
      <button class="${classes}" type="button" data-tag="${escapeAttribute(item.role)}" data-src="${escapeAttribute(src)}" data-role="${escapeAttribute(item.role)}" data-title="${escapeAttribute(item.title)}">
        <img src="${escapeAttribute(src)}" alt="${escapeAttribute(`${item.role} - ${item.title}`)}" loading="lazy" />
        <span>${escapeHtml(item.role)}</span>
        <h3>${escapeHtml(item.title)}</h3>
      </button>
    `;
  };

  grid.innerHTML = screenshotRoleOrder.map((role) => {
    const roleItems = screenshotItems.filter((item) => item.role === role);
    return `
      <section class="screenshot-role-group" data-role="${escapeAttribute(role)}">
        <div class="screenshot-role-heading">
          <h3>${escapeHtml(role)}</h3>
          <span>${roleItems.length} screenshots</span>
        </div>
        <div class="screenshot-role-grid">
          ${roleItems.map(renderCard).join("")}
        </div>
      </section>
    `;
  }).join("");

  const cards = [...grid.querySelectorAll(".screen-card")];
  const groups = [...grid.querySelectorAll(".screenshot-role-group")];

  const openLightbox = (card) => {
    if (!lightbox || !lightboxImage || !lightboxRole || !lightboxTitle) return;
    lightboxImage.src = card.dataset.src;
    lightboxImage.alt = `${card.dataset.role} - ${card.dataset.title}`;
    lightboxRole.textContent = card.dataset.role;
    lightboxTitle.textContent = card.dataset.title;
    lightbox.classList.add("open");
    lightbox.setAttribute("aria-hidden", "false");
  };

  const closeLightbox = () => {
    if (!lightbox || !lightboxImage) return;
    lightbox.classList.remove("open");
    lightbox.setAttribute("aria-hidden", "true");
    lightboxImage.src = "";
  };

  cards.forEach((card) => {
    card.addEventListener("click", () => openLightbox(card));
  });

  buttons.forEach((button) => {
    button.addEventListener("click", () => {
      buttons.forEach((item) => item.classList.toggle("active", item === button));
      const filter = button.dataset.filter || button.textContent.trim().toUpperCase();
      grid.classList.toggle("all-mode", filter === "ALL");
      groups.forEach((group) => {
        const visible = filter === "ALL" || group.dataset.role === filter;
        group.classList.toggle("hidden", !visible);
      });
    });
  });

  grid.classList.add("all-mode");

  closeButton?.addEventListener("click", closeLightbox);
  lightbox?.addEventListener("click", (event) => {
    if (event.target === lightbox) closeLightbox();
  });
  window.addEventListener("keydown", (event) => {
    if (event.key === "Escape") closeLightbox();
  });
}

function escapeHtml(value) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;");
}

function escapeAttribute(value) {
  return escapeHtml(value).replaceAll('"', "&quot;");
}

function span(className, value) {
  return `<span class="${className}">${escapeHtml(value)}</span>`;
}

function highlightWithGaps(source, pattern, renderToken) {
  let html = "";
  let cursor = 0;
  for (const match of source.matchAll(pattern)) {
    html += escapeHtml(source.slice(cursor, match.index));
    html += renderToken(match[0]);
    cursor = match.index + match[0].length;
  }
  html += escapeHtml(source.slice(cursor));
  return html;
}

function highlightCSharp(source) {
  const keywords = new Set([
    "abstract", "as", "async", "await", "base", "bool", "break", "case", "catch", "char",
    "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
    "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
    "foreach", "get", "global", "if", "implicit", "in", "int", "interface", "internal",
    "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out",
    "override", "params", "private", "protected", "public", "readonly", "record", "ref",
    "return", "sbyte", "sealed", "set", "short", "sizeof", "stackalloc", "static",
    "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint",
    "ulong", "unchecked", "unsafe", "ushort", "using", "var", "virtual", "void",
    "volatile", "while",
  ]);

  return highlightWithGaps(source, /\/\*[\s\S]*?\*\/|\/\/.*|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])'|\b[A-Za-z_][A-Za-z0-9_]*\b|\b\d+(?:\.\d+)?\b/g, (token) => {
    if (token.startsWith("//") || token.startsWith("/*")) return span("comment", token);
    if (token.startsWith('"') || token.startsWith('@"') || token.startsWith("'")) return span("str", token);
    if (/^\d/.test(token)) return span("num", token);
    if (keywords.has(token)) return span("kw", token);
    if (/^[A-Z][A-Za-z0-9_]*$/.test(token)) return span("type", token);
    return escapeHtml(token);
  });
}

function highlightSql(source) {
  const keywords = new Set([
    "add", "alter", "and", "as", "asc", "between", "by", "case", "constraint", "create",
    "database", "default", "delete", "desc", "drop", "exists", "foreign", "from", "group",
    "having", "if", "in", "index", "insert", "into", "is", "join", "key", "like", "limit",
    "not", "null", "on", "or", "order", "primary", "references", "select", "set", "table",
    "then", "unique", "update", "values", "varchar", "view", "when", "where",
  ]);

  return highlightWithGaps(source, /--.*|\/\*[\s\S]*?\*\/|'(?:''|[^'])*'|`[^`]*`|\b[A-Za-z_][A-Za-z0-9_]*\b|\b\d+(?:\.\d+)?\b/g, (token) => {
    if (token.startsWith("--") || token.startsWith("/*")) return span("comment", token);
    if (token.startsWith("'") || token.startsWith("`")) return span("str", token);
    if (/^\d/.test(token)) return span("num", token);
    if (keywords.has(token.toLowerCase())) return span("kw", token);
    return escapeHtml(token);
  });
}

function highlightMarkup(source) {
  return highlightWithGaps(source, /<!--[\s\S]*?-->|<\/?[A-Za-z][^>]*?>/g, (token) => {
    if (token.startsWith("<!--")) return span("comment", token);
    return span("kw", token);
  });
}

function highlightSource(file) {
  if (!file) return "";
  if (file.language === "csharp") return highlightCSharp(file.content);
  if (file.language === "sql") return highlightSql(file.content);
  if (file.language === "xaml" || file.language === "html") return highlightMarkup(file.content);
  return escapeHtml(file.content);
}

function iconForFile(fileName) {
  if (fileName.endsWith(".html")) return "html";
  if (fileName.endsWith(".sql")) return "database";
  if (fileName.endsWith(".xaml")) return "data_object";
  return "description";
}

function buildSourceTree(files) {
  const root = { name: "", folders: new Map(), files: [], parent: null };

  files.forEach((file) => {
    const parts = file.path.split("/");
    let node = root;
    parts.slice(0, -1).forEach((part) => {
      if (!node.folders.has(part)) node.folders.set(part, { name: part, folders: new Map(), files: [], parent: node });
      node = node.folders.get(part);
    });
    node.files.push(file);
  });

  return root;
}

function toggleFolder(row, childContainer, forceOpen) {
  const shouldOpen = typeof forceOpen === "boolean" ? forceOpen : childContainer.hidden;
  childContainer.hidden = !shouldOpen;
  row.classList.toggle("is-open", shouldOpen);
  row.querySelector(".folder-arrow").textContent = shouldOpen ? "keyboard_arrow_down" : "keyboard_arrow_right";
  row.querySelector(".folder-icon").textContent = shouldOpen ? "folder_open" : "folder";
}

function renderSourceNode(node, container, depth = 0, folderRows = new Map()) {
  [...node.folders.values()].forEach((folder) => {
    const row = document.createElement("button");
    const folderPath = [...folderPathParts(folder)].join("/");
    row.className = "tree-row folder-row";
    row.type = "button";
    row.dataset.folderPath = folderPath;
    row.style.setProperty("--tree-depth", depth);
    row.innerHTML = `<span class="material-symbols-outlined folder-arrow">keyboard_arrow_right</span><span class="material-symbols-outlined folder-icon">folder</span><span>${escapeHtml(folder.name)}</span>`;
    container.append(row);

    const childContainer = document.createElement("div");
    childContainer.className = "tree-indent active-folder";
    childContainer.style.setProperty("--tree-depth", depth + 1);
    childContainer.hidden = true;
    container.append(childContainer);
    row.addEventListener("click", () => toggleFolder(row, childContainer));
    folderRows.set(folderPath, { row, childContainer });
    renderSourceNode(folder, childContainer, depth + 1, folderRows);
  });

  node.files.forEach((file) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "tree-row file-row";
    button.dataset.path = file.path;
    button.style.setProperty("--tree-depth", depth);
    button.innerHTML = `<span class="material-symbols-outlined">${iconForFile(file.name)}</span><span>${escapeHtml(file.name)}</span>`;
    button.addEventListener("click", () => selectSourceFile(file.path));
    container.append(button);
  });

  return folderRows;
}

function* folderPathParts(folder) {
  const parts = [];
  let cursor = folder;
  while (cursor?.name) {
    parts.unshift(cursor.name);
    cursor = cursor.parent;
  }
  yield* parts;
}

function buildLineNumbers(lineCount) {
  return Array.from({ length: lineCount }, (_, index) => `<li>${index + 1}</li>`).join("");
}

function createSourcePanel(file) {
  const panel = document.createElement("div");
  panel.className = "source-code-panel";
  panel.dataset.path = file.path;
  panel.hidden = true;
  panel.innerHTML = `<ol class="line-numbers" aria-hidden="true">${buildLineNumbers(file.lineCount || 1)}</ol><pre><code>${highlightSource(file)}</code></pre>`;
  return panel;
}

function renderBreadcrumbs(file, projectName) {
  const crumbs = [projectName, ...file.path.split("/")];
  return crumbs.map((crumb, index) => {
    const safe = index === crumbs.length - 1 ? `<b>${escapeHtml(crumb)}</b>` : escapeHtml(crumb);
    return index === 0 ? safe : `<span>&gt;</span> ${safe}`;
  }).join(" ");
}

function selectSourceFile(path) {
  const data = window.FIRETRACK_SOURCE_DATA;
  if (!data) return;

  const file = data.files.find((candidate) => candidate.path === path) ?? data.files[0];
  if (!file) return;
  if (activeSourcePath === file.path) return;

  activeSourcePath = file.path;
  document.querySelectorAll(".file-row").forEach((row) => row.classList.toggle("selected", row.dataset.path === file.path));

  const lineCount = file.lineCount || 1;
  const codeWrap = document.querySelector(".code-wrap");
  const breadcrumbs = document.querySelector("#code-breadcrumbs");

  sourcePanels.forEach((panel, panelPath) => {
    panel.hidden = panelPath !== file.path;
  });
  breadcrumbs.innerHTML = renderBreadcrumbs(file, data.projectName);
  document.querySelector("#metric-lines").textContent = lineCount;
  codeWrap.scrollTo({ top: 0, left: 0 });
}

function initSourceExplorer() {
  const data = window.FIRETRACK_SOURCE_DATA;
  const tree = document.querySelector("#source-tree");
  const panels = document.querySelector("#code-panels");
  const rootToggle = document.querySelector("#source-root-toggle");
  if (!data || !tree || !panels) return;

  tree.innerHTML = "";
  panels.innerHTML = "";
  sourcePanels.clear();

  const sourceTree = buildSourceTree(data.files);
  renderSourceNode(sourceTree, tree);
  rootToggle?.addEventListener("click", () => toggleFolder(rootToggle, tree));

  data.files.forEach((file) => {
    const panel = createSourcePanel(file);
    sourcePanels.set(file.path, panel);
    panels.append(panel);
  });

  selectSourceFile(data.defaultPath || data.files[0]?.path);
}

document.addEventListener("DOMContentLoaded", () => {
  initRouter();
  initDocsJumps();
  initClock();
  initLeafletMap();
  initBarangayOrbs();
  initIncidentOrbs();
  initScreenshotFilters();
  initSourceExplorer();
});
