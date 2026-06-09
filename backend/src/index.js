import { createApp } from "./createApp.js";

const port = process.env.PORT || 3001;
const app = createApp();

app.listen(port, () => {
  console.log(`Appreciators TCG backend listening on port ${port}`);
});
