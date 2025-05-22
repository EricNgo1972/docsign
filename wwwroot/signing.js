window.signFromLocalService = async function (endpoint, payload) {
    const response = await fetch(`http://localhost:5005/${endpoint}`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: endpoint === "sign-pdf" ? JSON.stringify(payload) : payload
    });

    if (!response.ok) {
        return `Error: ${response.status}`;
    }

    return await response.text();
};
