//@flow
import React from 'react';
import { render } from 'react-dom';
import Chart from './Chart';
import getData from './getData'

class ChartComponent extends React.Component<{}, {data: Array<DataRow>, stats: Object}> {
	componentDidMount() {
		getData().then(data => {
			this.setState({data, stats: this.getStats(data)})
		})
	}
	render() {
		if (!this.state || !this.state.data) return <div>Loading...</div>
		return (<div>
							<Chart type={"hybrid"} data={this.state.data} />
							{this.state.stats}
					 </div>)
	}

	getStats = (data: Array<DataRow>) =>
		data
			.filter(row => data.backtestValue !== 0)
			.sort((a,b) => a.backtestValue - b.backtestValue)
			.filter((x, i) => i < 10)
			.map((row, i) => (<div key={i}>{JSON.stringify(row)}</div>))


}

//$FlowFixMe
render(<ChartComponent />, document.getElementById("root"));
