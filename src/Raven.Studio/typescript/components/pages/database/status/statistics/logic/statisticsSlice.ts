import { createAction, createAsyncThunk, createEntityAdapter, createSlice, EntityState } from "@reduxjs/toolkit";
import type { PayloadAction } from "@reduxjs/toolkit";
import { AppDispatch, pokemonApi, RootState } from "components/store";
import getDatabasesCommand from "commands/resources/getDatabasesCommand";
import { delay } from "components/utils/common";
import { fetchBaseQuery } from "@reduxjs/toolkit/query";
import { createApi } from "@reduxjs/toolkit/query/react";
import DatabasesInfo = Raven.Client.ServerWide.Operations.DatabasesInfo;
import { BaseThunkAPI } from "@reduxjs/toolkit/dist/createAsyncThunk";

export interface CounterState {
    value: number;
    dbs: string[];
    books2: EntityState<Book>;
}

type Book = { bookId: string; title: string };

const booksAdapter = createEntityAdapter<Book>({
    // Assume IDs are stored in a field other than `book.id`
    selectId: (book) => book.bookId,
    // Keep the "all IDs" array sorted based on book titles
    sortComparer: (a, b) => a.title.localeCompare(b.title),
});

const initialState: CounterState = {
    value: 0,
    dbs: [],
    books2: booksAdapter.getInitialState(),
};

type ThunkApiType = BaseThunkAPI<RootState, any, AppDispatch>;

export const fetchUserById = createAsyncThunk(
    "users/fetchByIdStatus",
    async (userId: number, thunkAPI: ThunkApiType) => {
        const cmd = new getDatabasesCommand();

        const s = thunkAPI.getState();
        
        const p = pokemonApi.endpoints.getPokemonByName.select("d")(s);
        await delay(2000);
        thunkAPI.dispatch(increment());
        await delay(2000);
        thunkAPI.dispatch(increment());
        const result = await cmd.execute();
        await delay(2000);

        thunkAPI.dispatch(increment());
        return result;
    }
);

export const counterSlice = createSlice({
    name: "counter",
    initialState,
    reducers: {
        increment: (state) => {
            state.value += 1;
        },
        decrement: (state) => {
            state.value -= 1;
        },
        incrementByAmount: (state, action: PayloadAction<number>) => {
            state.value += action.payload;
        },
        addBook: (state, action: PayloadAction<Book>) => {
            booksAdapter.addOne(state.books2, action.payload);
        },
    },
    extraReducers: (builder) => {
        builder.addCase(fetchUserById.fulfilled, (state, action) => {
            state.dbs = action.payload.Databases.map((x) => x.Name);
        });
    },
});

// Action creators are generated for each case reducer function

export const { increment, decrement, incrementByAmount, addBook } = counterSlice.actions;

export const selectCount = (state: RootState) => state.stats.value;
export const selectDatabases = (state: RootState) => state.stats.dbs;

export default counterSlice.reducer;
