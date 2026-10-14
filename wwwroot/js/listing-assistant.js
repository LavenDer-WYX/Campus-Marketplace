
(function () {
    "use strict";


    const panel = document.getElementById("listing-assistant");
    if (!panel) return;

    if (panel.dataset.ready === "true") return;
    panel.dataset.ready = "true";


    const form = panel.closest("form");
    if (!form) return;

    const title = form.querySelector("#Title");
    const category = form.querySelector("#CategoryId");
    const condition = form.querySelector("#Condition");
    const description = form.querySelector("#Description");
    const price = form.querySelector("#Price");
    const listingType = form.querySelector("#ListingType");

    if (!title || !category || !condition || !description || !price || !listingType) return;


    const generateButton = document.getElementById("assistant-generate");
    const useDraftButton = document.getElementById("assistant-use-draft");
    const priceButton = document.getElementById("assistant-price");
    const usePriceButton = document.getElementById("assistant-use-price");
    const draftArea = document.getElementById("assistant-draft-area");
    const draft = document.getElementById("assistant-draft");
    const descriptionStatus = document.getElementById("assistant-description-status");
    const priceStatus = document.getElementById("assistant-price-status");

    let suggestedPrice = null;

    let priceCategory = "";
    let priceCondition = "";
    let priceTitle = "";
    let priceAge = "";
    let priceAccessories = "";


    async function postForm(url, values) {

        const token = form.querySelector('input[name="__RequestVerificationToken"]');
        if (!token) throw new Error("Please refresh this page before using the listing assistant.");

        const body = new URLSearchParams(values);

        const cancellation = new AbortController();
        const timer = window.setTimeout(function () { cancellation.abort(); }, 25000);
        try {

            const response = await fetch(url, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "RequestVerificationToken": token.value,
                    "Accept": "application/json"
                },
                body: body,
                signal: cancellation.signal
            });

            if (response.redirected || response.status === 401 || response.status === 403) {
                throw new Error("Please sign in again, then reopen this listing form.");
            }

            if (response.status === 429) {
                throw new Error("Too many draft requests. Please wait a minute and try again.");
            }

            const contentType = response.headers.get("content-type") || "";
            if (!contentType.includes("application/json")) {
                throw new Error("The request could not be processed. Refresh the page and try again.");
            }

            const data = await response.json();

            if (!response.ok || data.success !== true) {
                throw new Error(data.message || "The listing assistant is unavailable. Please try again later.");
            }

            return data;
        } finally {

            window.clearTimeout(timer);
        }
    }


    generateButton.addEventListener("click", async function () {

        if (!title.value.trim() || !category.value || !condition.value) {
            descriptionStatus.textContent = "Enter a title, category and condition first.";
            return;
        }

        generateButton.disabled = true;
        draftArea.hidden = true;
        descriptionStatus.textContent = "Generating a draft. Please wait...";
        try {

            const data = await postForm(panel.dataset.generateUrl, {
                Title: title.value,
                CategoryId: category.value,
                Condition: condition.value,
                Age: document.getElementById("assistant-age").value,
                Accessories: document.getElementById("assistant-accessories").value
            });

            draft.value = data.description;
            draftArea.hidden = false;
            descriptionStatus.textContent = data.message;
        } catch (error) {

            descriptionStatus.textContent = error.name === "AbortError"
                ? "The request timed out. Please try again later."
                : error.message;
        } finally {

            generateButton.disabled = false;
        }
    });


    useDraftButton.addEventListener("click", function () {

        if (description.value.trim() && !window.confirm("Replace your current description with this draft?")) return;

        description.value = draft.value;

        description.dispatchEvent(new Event("input", { bubbles: true }));
        descriptionStatus.textContent = "Draft copied to the description. Review it before saving your listing.";
        description.focus();
    });


    priceButton.addEventListener("click", async function () {

        if (listingType.value !== "Sale") {
            priceStatus.textContent = "AI price suggestions are for Sale listings only.";
            return;
        }

        if (!title.value.trim() || !category.value || !condition.value) {
            priceStatus.textContent = "Enter a title, category and condition first.";
            return;
        }

        const requestedCategory = category.value;
        const requestedCondition = condition.value;
        const requestedTitle = title.value.trim();
        const requestedAge = document.getElementById("assistant-age").value.trim();
        const requestedAccessories = document.getElementById("assistant-accessories").value.trim();

        suggestedPrice = null;
        usePriceButton.hidden = true;
        priceButton.disabled = true;
        priceStatus.textContent = "Generating an AI price suggestion. Please wait...";
        try {

            const data = await postForm(panel.dataset.priceUrl, {
                Title: requestedTitle,
                CategoryId: requestedCategory,
                Condition: requestedCondition,
                Age: requestedAge,
                Accessories: requestedAccessories
            });

            const result = data.result;
            priceStatus.textContent = result.message;

            if (result.success && result.suggestedPrice !== null) {

                suggestedPrice = Number(result.suggestedPrice);
                priceCategory = requestedCategory;
                priceCondition = requestedCondition;
                priceTitle = requestedTitle;
                priceAge = requestedAge;
                priceAccessories = requestedAccessories;
                let text = "AI suggestion: NZ$" + suggestedPrice.toFixed(2) +
                    ". Suggested range: NZ$" + Number(result.suggestedMinimumPrice).toFixed(2) +
                    "–NZ$" + Number(result.suggestedMaximumPrice).toFixed(2) + ". ";

                if (result.observedMinimumPrice !== null && result.observedMaximumPrice !== null) {
                    text += "Local asking-price range: NZ$" + Number(result.observedMinimumPrice).toFixed(2) +
                        "–NZ$" + Number(result.observedMaximumPrice).toFixed(2) +
                        " from " + result.sampleCount + " listing(s). ";
                }
                priceStatus.textContent = text + result.message;
                usePriceButton.hidden = false;
            }
        } catch (error) {

            priceStatus.textContent = error.name === "AbortError"
                ? "The request timed out. Please try again later."
                : error.message;
        } finally {

            priceButton.disabled = false;
        }
    });


    usePriceButton.addEventListener("click", function () {

        if (listingType.value !== "Sale" || category.value !== priceCategory || condition.value !== priceCondition ||
            title.value.trim() !== priceTitle ||
            document.getElementById("assistant-age").value.trim() !== priceAge ||
            document.getElementById("assistant-accessories").value.trim() !== priceAccessories) {
            priceStatus.textContent = "Your item details changed. Generate a new AI price suggestion.";
            usePriceButton.hidden = true;
            return;
        }

        if (suggestedPrice === null) return;

        if (price.value && !window.confirm("Replace your current price with this suggested price?")) return;

        price.value = suggestedPrice.toFixed(2);
        price.dispatchEvent(new Event("input", { bubbles: true }));
        price.focus();
    });
})();
