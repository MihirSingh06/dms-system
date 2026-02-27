import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend
} from "chart.js";

import { Bar } from "react-chartjs-2";

ChartJS.register(
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend
);

function SpendChart({ data }) {
  if (!data) return null;

  const chartData = {
    labels: Object.keys(data),
    datasets: [
      {
        label: "Total Spend",
        data: Object.values(data),
        backgroundColor: "#2563eb"
      }
    ]
  };

  const options = {
    responsive: true,
    plugins: {
      legend: { display: false }
    }
  };

  return (
    <div style={{ height: 350 }}>
      <Bar data={chartData} options={options} />
    </div>
  );
}

export default SpendChart;