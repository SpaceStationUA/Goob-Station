import { render } from "solid-js/web";
import App from "./App";
import { installTestHooks } from "./dev-hooks";
import "./galaxy.css";

// Before the tree mounts: the checks read these the moment `svg.chart` appears,
// and a hook installed in an `onMount` can land a frame later.
installTestHooks();

render(() => <App />, document.getElementById("root")!);
