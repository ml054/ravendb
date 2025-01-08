import { composeStory } from "@storybook/react";
import * as stories from "components/pages/database/tasks/ongoingTasks/stories/ReplicationSink.stories";
import { rtlRender } from "test/rtlTestUtils";
import { within } from "@testing-library/dom";
import React from "react";

const containerTestId = "replication-sinks";

describe("Replication Sink", function () {
    it("can render enabled", async () => {
        const Story = composeStory(stories.Default, stories.default);

        const { screen, fireClick } = rtlRender(<Story />);
        const container = within(await screen.findByTestId(containerTestId));
        expect(await container.findByRole("heading", { name: /Replication Sink/ })).toBeInTheDocument();
        expect(await container.findByText(/Enabled/)).toBeInTheDocument();
        expect(container.queryByText(/Disabled/)).not.toBeInTheDocument();

        const detailsBtn = await container.findByTitle(/Click for details/);

        await fireClick(detailsBtn);

        expect(await container.findByText(/Hub Database/)).toBeInTheDocument();
        expect(await container.findByText(/Connection String/)).toBeInTheDocument();
        expect(await container.findByText(/Actual Hub URL/)).toBeInTheDocument();
        expect(await container.findByText(/Hub Name/)).toBeInTheDocument();
    });
});
