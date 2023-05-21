window.chartExtensions = {
	FILL_MAIN_BARCHART: function (months, studyCounts, label) {
		var barChartData = {
			labels: months,
			datasets: [{
				label: label,
				backgroundColor: 'rgba(85, 206, 99, 0.5)',
				borderColor: 'rgba(85, 206, 99, 1)',
				borderWidth: 1,
				data: studyCounts
			}]
		};

		var ctx = document.getElementById('mainbargraph').getContext('2d');
		window.myBar = new Chart(ctx, {
			type: 'bar',
			data: barChartData,
			options: {
				responsive: true,
				legend: {
					display: false,
				},
				scales: {
					yAxes: [{
						ticks: {
							beginAtZero: true
						}
					}]
				}
			}
		});
	}
}
