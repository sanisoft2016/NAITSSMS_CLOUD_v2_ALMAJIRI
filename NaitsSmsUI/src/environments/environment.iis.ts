const apiPort = 8090;

function lanApiHost(): string {
  const host = window.location.hostname || 'localhost';
  return host.includes(':') ? `[${host}]` : host;
}

export const environment = {
  production: true,
  fileUploadMaxSize: 1048576,
  allowedUploadExtentions: ['pdf', 'docx', 'doc', 'png', 'jpg', 'jpeg'],

  get serverUrl(): string {
    return `http://${lanApiHost()}:${apiPort}/api`;
  },

  get baseURL(): string {
    return new URL(window.location.href).origin;
  },
  useHash: false,
};
