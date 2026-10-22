
import { useEffect, useState } from "react";

function App() {
  const [result, setResult] = useState("در حال دریافت پاسخ...");
  const [error, setError] = useState("");

  useEffect(() => {
    fetch("http://localhost:8082/health")
        .then(async (response) => {
          if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
          }
          return response.json();
        })
        .then((data) => setResult(JSON.stringify(data)))
        .catch((err) => setError(err.message));
  }, []);

  return (
      <main>
        <h1>Lab 4 - Backend Weather Check</h1>
        {error ? <p>Error: {error}</p> : <p>{result}</p>}
      </main>
  );
}

export default App;
