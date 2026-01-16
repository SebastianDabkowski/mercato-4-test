// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener("DOMContentLoaded", () => {
    const searchInput = document.querySelector('[data-testid="global-search-input"]');
    if (!searchInput) {
        return;
    }

    const form = searchInput.closest("form");
    const suggestionList = form?.querySelector("[data-suggestion-list]");
    const endpoint = searchInput.dataset.suggestionUrl;
    const minLength = Number.parseInt(searchInput.dataset.suggestionMinLength ?? "2", 10);
    const debounceMs = Number.parseInt(searchInput.dataset.suggestionDebounce ?? "250", 10);

    if (!form || !suggestionList || !endpoint) {
        return;
    }

    let debounceHandle;
    let activeController;
    let requestCounter = 0;

    const hideSuggestions = () => {
        suggestionList.innerHTML = "";
        suggestionList.classList.add("d-none");
        suggestionList.hidden = true;
        searchInput.setAttribute("aria-expanded", "false");
    };

    const showSuggestions = () => {
        suggestionList.classList.remove("d-none");
        suggestionList.hidden = false;
        searchInput.setAttribute("aria-expanded", "true");
    };

    const buildSectionHeader = (title) => {
        const header = document.createElement("div");
        header.className = "list-group-item text-uppercase text-muted small";
        header.textContent = title;
        return header;
    };

    const buildQueryItem = (query) => {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "list-group-item list-group-item-action";
        button.textContent = query;
        button.setAttribute("data-testid", "search-suggestion-query");
        button.addEventListener("click", () => {
            searchInput.value = query;
            hideSuggestions();
            form.submit();
        });
        return button;
    };

    const buildCategoryItem = (category) => {
        const link = document.createElement("a");
        link.className = "list-group-item list-group-item-action";
        link.href = category.url;
        link.textContent = category.label;
        link.setAttribute("data-testid", "search-suggestion-category");
        return link;
    };

    const buildProductItem = (product) => {
        const link = document.createElement("a");
        link.className = "list-group-item list-group-item-action";
        link.href = product.url;
        link.setAttribute("data-testid", "search-suggestion-product");

        const title = document.createElement("div");
        title.textContent = product.label;
        link.appendChild(title);

        if (product.category) {
            const category = document.createElement("small");
            category.className = "text-muted";
            category.textContent = product.category;
            link.appendChild(category);
        }

        return link;
    };

    const renderSuggestions = (data) => {
        suggestionList.innerHTML = "";

        const queries = data?.queries ?? [];
        const categories = data?.categories ?? [];
        const products = data?.products ?? [];

        if (!queries.length && !categories.length && !products.length) {
            const empty = document.createElement("div");
            empty.className = "list-group-item text-muted";
            empty.textContent = "No suggestions found.";
            empty.setAttribute("data-testid", "search-suggestion-empty");
            suggestionList.appendChild(empty);
            showSuggestions();
            return;
        }

        if (queries.length) {
            suggestionList.appendChild(buildSectionHeader("Suggested searches"));
            queries.forEach((query) => suggestionList.appendChild(buildQueryItem(query)));
        }

        if (categories.length) {
            suggestionList.appendChild(buildSectionHeader("Categories"));
            categories.forEach((category) => suggestionList.appendChild(buildCategoryItem(category)));
        }

        if (products.length) {
            suggestionList.appendChild(buildSectionHeader("Products"));
            products.forEach((product) => suggestionList.appendChild(buildProductItem(product)));
        }

        showSuggestions();
    };

    const fetchSuggestions = (value) => {
        if (activeController) {
            activeController.abort();
        }

        activeController = new AbortController();
        const currentRequest = ++requestCounter;
        const url = new URL(endpoint, window.location.origin);
        url.searchParams.set("term", value);

        fetch(url, { signal: activeController.signal, headers: { Accept: "application/json" } })
            .then((response) => {
                if (!response.ok) {
                    throw new Error("Suggestion request failed");
                }
                return response.json();
            })
            .then((data) => {
                if (currentRequest !== requestCounter) {
                    return;
                }
                renderSuggestions(data);
            })
            .catch((error) => {
                if (error.name === "AbortError") {
                    return;
                }
                hideSuggestions();
            });
    };

    const handleInput = () => {
        const value = searchInput.value.trim();
        if (value.length < minLength) {
            if (activeController) {
                activeController.abort();
            }
            hideSuggestions();
            return;
        }

        if (debounceHandle) {
            clearTimeout(debounceHandle);
        }

        debounceHandle = setTimeout(() => fetchSuggestions(value), debounceMs);
    };

    searchInput.addEventListener("input", handleInput);
    searchInput.addEventListener("focus", handleInput);
    searchInput.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            hideSuggestions();
        }
    });

    form.addEventListener("submit", hideSuggestions);
    document.addEventListener("click", (event) => {
        if (!form.contains(event.target)) {
            hideSuggestions();
        }
    });
});
