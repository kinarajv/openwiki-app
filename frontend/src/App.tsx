import { BrowserRouter as Router, Routes, Route } from "react-router-dom";
import mermaid from "mermaid";
import { HomePage } from "./pages/HomePage";
import { DocumentationPage } from "./pages/DocumentationPage";

mermaid.initialize({ startOnLoad: false, theme: "neutral" });

function App() {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/wiki/:owner/:repo/:sectionSlug?" element={<DocumentationPage />} />
      </Routes>
    </Router>
  );
}

export default App;
