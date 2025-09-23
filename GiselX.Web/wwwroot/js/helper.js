const numberFormat = Intl.NumberFormat("en-US", { style: 'decimal', minimumFractionDigits: 0, maximumFractionDigits: 0 });
const numberFormat2Decimals = Intl.NumberFormat("en-US", { style: 'decimal', minimumFractionDigits: 2, maximumFractionDigits: 2 });
const percentFormat = Intl.NumberFormat('en-US', { style: 'percent', minimumFractionDigits: 1, maximumFractionDigits: 1 });
const percentFormat2Decimals = Intl.NumberFormat('en-US', { style: 'percent', minimumFractionDigits: 2, maximumFractionDigits: 2 });

function debounce(func, delay) {
    let timeout;
    return function(...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), delay);
    };
}