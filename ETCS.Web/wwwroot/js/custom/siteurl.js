var SiteUrl = '';
if (!location.origin)
    location.origin = (location.protocol + "//" + location.host + "/");

if (location.origin.indexOf("localhost") >= 0) {
    SiteUrl = location.origin + "/";
}
else {
    if ((location.origin.lastIndexOf('/')) === location.origin.length + 1) {
        SiteUrl = location.origin.substring(location.origin.indexOf('/'), location.origin.lastIndexOf('/'));
    } else {
        SiteUrl = location.origin + "/";
    }
}