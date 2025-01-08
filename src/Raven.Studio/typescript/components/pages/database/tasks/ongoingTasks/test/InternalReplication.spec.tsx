import { composeStory } from "@storybook/react";
import * as stories from "components/pages/database/tasks/ongoingTasks/stories/InternalReplication.stories";
import { rtlRender } from "test/rtlTestUtils";
import React from "react";

describe("Internal Replication", function () {
    it("can render", async () => {
        const Story = composeStory(stories.Default, stories.default);

        const { screen, fireClick } = rtlRender(<Story />);
        const container = screen;
        expect(await container.findByRole("heading", { name: /Internal Replication/ })).toBeInTheDocument();

        const detailsBtn = await container.findByTitle(/Click for details/);

        await fireClick(detailsBtn);

        expect(await container.findByText(/Last DB Etag/)).toBeInTheDocument();
        expect(await container.findByText(/Last Sent Etag/)).toBeInTheDocument();
    });
});
