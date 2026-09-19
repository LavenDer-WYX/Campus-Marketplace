// 把变量限制在这个函数里，避免与原来的 site.js 同名变量互相影响。
(function () {
    "use strict";

    // 找到助手区域；其他页面没有这个区域，直接结束就可以。
    const panel = document.getElementById("listing-assistant");
    if (!panel) return;
    // 防止页面意外重复加载这个脚本时，给一个按钮绑定两次请求。
    if (panel.dataset.ready === "true") return;
    panel.dataset.ready = "true";

    // closest 找到包住助手的商品表单，后面从这里读取防伪令牌。
    const form = panel.closest("form");
    if (!form) return;
    // 这些 ID 来自原有的 asp-for 属性，直接复用商品表单已有输入。
    const title = form.querySelector("#Title");
    const category = form.querySelector("#CategoryId");
    const condition = form.querySelector("#Condition");
    const description = form.querySelector("#Description");
    const price = form.querySelector("#Price");
    const listingType = form.querySelector("#ListingType");
    // 某个必要输入框缺失时不继续操作，避免运行时空对象错误。
    if (!title || !category || !condition || !description || !price || !listingType) return;

    // 缓存按钮和提示区域，点击时就不需要再次寻找它们。
    const generateButton = document.getElementById("assistant-generate");
    const useDraftButton = document.getElementById("assistant-use-draft");
    const priceButton = document.getElementById("assistant-price");
    const usePriceButton = document.getElementById("assistant-use-price");
    const draftArea = document.getElementById("assistant-draft-area");
    const draft = document.getElementById("assistant-draft");
    const descriptionStatus = document.getElementById("assistant-description-status");
    const priceStatus = document.getElementById("assistant-price-status");
    // 记录当前展示的价格参考；没有成功获取时保持 null。
    let suggestedPrice = null;
    // 记录参考使用的分类和新旧程度，避免后来改选分类后误用旧结果。
    let priceCategory = "";
    let priceCondition = "";

    // 两个按钮都使用同一个小函数发送表单编码的 POST 请求。
    async function postForm(url, values) {
        // Razor 的外层 form 会自动生成这个隐藏输入框。
        const token = form.querySelector('input[name="__RequestVerificationToken"]');
        if (!token) throw new Error("Please refresh this page before using the listing assistant.");
        // URLSearchParams 将简单字段编码为服务器可以接收的表单数据。
        const body = new URLSearchParams(values);
        // 控制浏览器最长等待 25 秒，略大于服务器自己的 20 秒超时。
        const cancellation = new AbortController();
        const timer = window.setTimeout(function () { cancellation.abort(); }, 25000);
        try {
            // credentials 只给本站发送登录 Cookie；Key 永远不会出现在这个脚本里。
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
            // Cookie 过期时，MVC 可能跳转到登录 HTML 页面，不应尝试当 JSON 解析。
            if (response.redirected || response.status === 401 || response.status === 403) {
                throw new Error("Please sign in again, then reopen this listing form.");
            }
            // 本站的限流器返回 429，让用户知道需要等待。
            if (response.status === 429) {
                throw new Error("Too many draft requests. Please wait a minute and try again.");
            }
            // 防伪令牌失效或其他错误可能返回 HTML，先检查 Content-Type。
            const contentType = response.headers.get("content-type") || "";
            if (!contentType.includes("application/json")) {
                throw new Error("The request could not be processed. Refresh the page and try again.");
            }
            // 将 JSON 字符串转换成普通 JavaScript 对象。
            const data = await response.json();
            // HTTP 错误和业务错误都显示服务器提供的简短说明。
            if (!response.ok || data.success !== true) {
                throw new Error(data.message || "The listing assistant is unavailable. Please try again later.");
            }
            // 正常返回给调用的按钮事件。
            return data;
        } finally {
            // 无论成功还是失败，都移除定时器，避免请求结束后继续触发取消。
            window.clearTimeout(timer);
        }
    }

    // 只根据明确的按钮点击生成草稿，不会在输入文字时自动调用付费接口。
    generateButton.addEventListener("click", async function () {
        // 先检查最基本的字段；服务器仍然会再做一次完整验证。
        if (!title.value.trim() || !category.value || !condition.value) {
            descriptionStatus.textContent = "Enter a title, category and condition first.";
            return;
        }
        // 请求期间禁止重复点击，并隐藏上一份草稿避免误用。
        generateButton.disabled = true;
        draftArea.hidden = true;
        descriptionStatus.textContent = "Generating a draft. Please wait...";
        try {
            // 只发送下列商品字段，不收集用户资料或页面中的其他输入。
            const data = await postForm(panel.dataset.generateUrl, {
                Title: title.value,
                CategoryId: category.value,
                Condition: condition.value,
                Age: document.getElementById("assistant-age").value,
                Accessories: document.getElementById("assistant-accessories").value
            });
            // 通过 textarea.value 显示纯文本，不使用 innerHTML，避免把 AI 文本当网页运行。
            draft.value = data.description;
            draftArea.hidden = false;
            descriptionStatus.textContent = data.message;
        } catch (error) {
            // 用户看到友好错误后仍然可以自己填写原来的商品描述。
            descriptionStatus.textContent = error.name === "AbortError"
                ? "The request timed out. Please try again later."
                : error.message;
        } finally {
            // 请求结束以后恢复按钮。
            generateButton.disabled = false;
        }
    });

    // 生成草稿不等于采用草稿；只有点这里才会替换描述输入框。
    useDraftButton.addEventListener("click", function () {
        // 原来已经写了描述时，再明确提醒一次将被替换。
        if (description.value.trim() && !window.confirm("Replace your current description with this draft?")) return;
        // 写入表单但不提交商品，用户还可以继续修改。
        description.value = draft.value;
        // 通知可能存在的表单验证逻辑：输入框内容已经改变。
        description.dispatchEvent(new Event("input", { bubbles: true }));
        descriptionStatus.textContent = "Draft copied to the description. Review it before saving your listing.";
        description.focus();
    });

    // 价格参考与 AI 请求完全分开，没有配置 AI Key 也能运行。
    priceButton.addEventListener("click", async function () {
        // 换物和赠送不需要给出售价。
        if (listingType.value !== "Sale") {
            priceStatus.textContent = "Price references are for Sale listings only.";
            return;
        }
        // 必须选择分类和新旧程度才能比较挂牌价。
        if (!category.value || !condition.value) {
            priceStatus.textContent = "Choose a category and condition first.";
            return;
        }
        // 把请求时的条件存起来，稍后应用结果时再次核对。
        const requestedCategory = category.value;
        const requestedCondition = condition.value;
        // 清除上一次建议，避免查询失败后还误用旧价格。
        suggestedPrice = null;
        usePriceButton.hidden = true;
        priceButton.disabled = true;
        priceStatus.textContent = "Checking asking prices in the database...";
        try {
            // 价格查询只发送分类和新旧程度。
            const data = await postForm(panel.dataset.priceUrl, {
                CategoryId: requestedCategory,
                Condition: requestedCondition
            });
            // 服务的返回对象位于 result 中。
            const result = data.result;
            priceStatus.textContent = result.message;
            // 没有样本时只显示说明，不给用户编造一个金额。
            if (result.hasData) {
                // 转换为 Number，接下来统一显示两位小数。
                suggestedPrice = Number(result.suggestedPrice);
                priceCategory = requestedCategory;
                priceCondition = requestedCondition;
                // 同时显示样本数量、平均价与实际样本范围，避免把均价说成精确估价。
                priceStatus.textContent = "Average: NZ$" + suggestedPrice.toFixed(2) +
                    ". Observed range: NZ$" + Number(result.minimumPrice).toFixed(2) +
                    "–NZ$" + Number(result.maximumPrice).toFixed(2) +
                    ". Sample count: " + result.sampleCount + ". " + result.message;
                usePriceButton.hidden = false;
            }
        } catch (error) {
            // 查询错误不会改动原来输入的价格。
            priceStatus.textContent = error.name === "AbortError"
                ? "The request timed out. Please try again later."
                : error.message;
        } finally {
            // 查询结束恢复按钮。
            priceButton.disabled = false;
        }
    });

    // 只有明确点击 Use suggested price，才把参考价填入商品表单。
    usePriceButton.addEventListener("click", function () {
        // 用户可能在查询后改了分类、新旧程度或商品类型，旧结果就不能直接套用。
        if (listingType.value !== "Sale" || category.value !== priceCategory || condition.value !== priceCondition) {
            priceStatus.textContent = "Your listing options changed. Check the price reference again.";
            usePriceButton.hidden = true;
            return;
        }
        // 没有参考值时不做任何修改。
        if (suggestedPrice === null) return;
        // 原来有价格时请用户确认替换，防止误点丢失自己输入的数值。
        if (price.value && !window.confirm("Replace your current price with this suggested price?")) return;
        // 只改变输入框，最终保存仍由原来的 ListingsController 处理。
        price.value = suggestedPrice.toFixed(2);
        price.dispatchEvent(new Event("input", { bubbles: true }));
        price.focus();
    });
})();
