const applicationNamespace = "ns://conferenceBooking/";

$(".modal").on("show.bs.modal", (evt) => {
    const related = evt.relatedTarget;
    const modal = evt.target;
    
    let context = related;
    let contextSrc = modal.dataset["context"];
    if (!!contextSrc) {
        context = findElementByXPath(contextSrc, context);
    }
    
    clearInputs(modal);

    applyDataUpdates(modal, context, "@data-value-from", (e, v) => e.value = v);
    applyDataUpdates(modal, context, "@data-text-from", (e, v) => e.innerText = v);
});

const findElementsByXPath = (expression, context = document) => {
    const elements = document.evaluate(expression, context, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
    let result = [];
    for (let i = 0; i < elements.snapshotLength; i++)
        result.push(elements.snapshotItem(i))
    return result;
}

const findStringByXPath = (expression, context = document) => 
    document.evaluate(expression, context, null, XPathResult.STRING_TYPE, null).stringValue;

const findElementByXPath = (expression, context = document) =>
    document.evaluate(expression, context, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;

const clearInputs = modal => findElementsByXPath(".//input", modal).forEach(e => e.value="");

const applyDataUpdates = (target, context, selector, mutator) => {
    const updates = findElementsByXPath(`.//*[${selector}]`, target);
    updates.forEach(u => mutator(u, findStringByXPath(findStringByXPath(selector, u), context)));
}

$('.modal[data-post-endpoint] button[type=submit]').on('click', (evt) => {
    const modal = $(evt.target).closest('.modal');
    const endpoint = modal.data("postEndpoint");
    let payload = {}
    
    applyGeneratedInputs(modal);
    
    for (const input of modal.find('input')) {
        const $input = $(input);
        payload[$input.attr('name')] = $input.val();
    }

    const jsonPayload = JSON.stringify(payload);
    
    $.ajax({
        url: endpoint,
        type: "post",
        data: jsonPayload,
        contentType: "application/json"
    })
        .always(_ => {
            modal.modal('hide');
        })
})

const applyGeneratedInputs = (modal) => {
    applyGeneratedInputsFor(modal,'data-guid-from', userGuid)
}

const applyGeneratedInputsFor = (modal, dataTag, mutator) => {
    for (const elem of modal.find(`[${dataTag}]`)) {
        const $elem = $(elem);
        const selector = $elem.attr(dataTag);
        const value = $(selector).val();
        const mutated = mutator(value);
        $elem.val(mutated);
    }
} 

const userGuid = (username) => toGuidV5(`user:${username}`, applicationNamespace);

function toGuidV5(value, nameSpace = "ns://default") {
    // Use CryptoJS for SHA-1 hashing
    const hash = CryptoJS.SHA1(nameSpace + value);

    // Convert WordArray to bytes
    const hashBytes = [];
    for (let i = 0; i < hash.words.length; i++) {
        const word = hash.words[i];
        hashBytes.push((word >> 24) & 0xff);
        hashBytes.push((word >> 16) & 0xff);
        hashBytes.push((word >> 8) & 0xff);
        hashBytes.push(word & 0xff);
    }

    // Apply the version and variant bits
    hashBytes[6] = (hashBytes[6] & 0x0f) | 0x50;
    hashBytes[8] = (hashBytes[8] & 0x3f) | 0x80;

    // Format as GUID
    const hexValues = hashBytes.slice(0, 16).map(b =>
        b.toString(16).padStart(2, '0')
    );

    return [
        hexValues.slice(0, 4).join(''),
        hexValues.slice(4, 6).join(''),
        hexValues.slice(6, 8).join(''),
        hexValues.slice(8, 10).join(''),
        hexValues.slice(10, 16).join('')
    ].join('-');
}