// This file can be replaced during build by using the `fileReplacements` array.
// `ng build --prod` replaces `environment.ts` with `environment.prod.ts`.
// The list of file replacements can be found in `angular.json`.

const newLocal = 'https://localhost:7049/api'; // proxied by proxy.config.js in development
export const environment = {
    production: false,
    serverUrl: newLocal,
    fileUploadMaxSize: 1048576,
    allowedUploadExtentions: ['pdf', 'docx', 'doc', 'png', 'jpg', 'jpeg'],
    get baseURL(): string {
        const parsedUrl = new URL(window.location.href);
        let baseUrl = parsedUrl.origin;
        return baseUrl;
    },
    useHash: false,
};

/*
 * For easier debugging in development mode, you can import the following file
 * to ignore zone related error stack frames such as `zone.run`, `zoneDelegate.invokeTask`.
 *
 * This import should be commented out in production mode because it will have a negative impact
 * on performance if an error is thrown.
 */
// import 'zone.js/plugins/zone-error';  // Included with Angular CLI.
